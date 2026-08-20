<template>
    <div
        class="neon-terminal"
        :class="{
            'neon-terminal--still': prefersReducedMotion,
            'neon-terminal--alive': isAlive,
            'neon-terminal--dying': isCollapsing,
            'neon-terminal--dimming': isDimming,
            'neon-terminal--dormant': isDormant
        }"
    >
        <!-- separate frame layer so the border can flicker without dragging the text along -->
        <span ref="frame" class="neon-terminal__frame" aria-hidden="true"></span>

        <div class="neon-terminal__bar">
            <button
                v-if="!prefersReducedMotion"
                type="button"
                class="neon-terminal__btn neon-terminal__btn--skip"
                :class="{ 'neon-terminal__btn--faulty': isAlive }"
                :disabled="finished"
                @click="skipIntro"
            >skip</button>
            <button
                type="button"
                class="neon-terminal__btn neon-terminal__btn--copy"
                :disabled="isDying || isDormant"
                @click="copyLink"
            >{{ copied ? 'copied' : 'copy' }}</button>
        </div>

        <div class="neon-terminal__body">
            <p
                v-for="(line, index) in typedLines"
                :key="index"
                class="neon-terminal__line"
                :class="{
                    'neon-terminal__line--faulty': isAlive && line.flicker,
                    'neon-terminal__line--keep': line.keep
                }"
                :style="{ '--die-delay': dieDelay(index) }"
            >
                <span class="neon-terminal__prompt">&gt;</span>
                <a
                    v-if="line.href && line.complete"
                    class="neon-terminal__link"
                    :href="line.href"
                    target="_blank"
                    rel="noopener noreferrer"
                >{{ line.typed }}</a>
                <span v-else class="neon-terminal__text">{{ line.typed }}</span>
                <span v-if="index === cursorIndex" class="neon-terminal__caret" aria-hidden="true"></span>
            </p>
        </div>
    </div>
</template>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, ref, watch } from 'vue'
import { useTypewriter, type TerminalLine } from '../composables/useTypewriter'

const props = withDefaults(defineProps<{
    /** output lines, in order */
    lines: TerminalLine[]
    /** what the copy button puts on the clipboard */
    copyText?: string
    /** skip phase 1+2 and open straight in the finished, living state (used when re-lighting) */
    instant?: boolean
    /**
     * how the terminal dies.
     * 'collapse' - everything goes out and the box folds in on itself (share flow)
     * 'dim'      - everything goes out except the line marked `keep`, which stays
     *              behind on low power; the box keeps its place (pickup flow)
     */
    death?: 'collapse' | 'dim'
    /** ms after the output is complete before it dies by itself; 0 disables it */
    autoDeath?: number
}>(), {
    copyText: '',
    instant: false,
    death: 'collapse',
    autoDeath: 0
})

const emit = defineEmits<{
    (e: 'copy'): void
    (e: 'done'): void
    (e: 'dead'): void
}>()

/**
 * typing  -> phase 1+2: frame powers on, THEN the output reveals itself
 * alive   -> phase 3: steady glow, with two off-beat "broken tubes"
 * dying   -> phase 4: elements go out in an uneven order
 * dead    -> collapse death only: gone, the parent hides it and can re-mount it
 * dormant -> dim death only: still on screen, only the kept line slumbering
 */
type Phase = 'typing' | 'alive' | 'dying' | 'dead' | 'dormant'
const phase = ref<Phase>('typing')

const isAlive = computed(() => phase.value === 'alive')
const isDying = computed(() => phase.value === 'dying')
const isDormant = computed(() => phase.value === 'dormant')
const isCollapsing = computed(() => isDying.value && props.death === 'collapse')
const isDimming = computed(() => isDying.value && props.death === 'dim')

/* --- death choreography (ms) --------------------------------------------
   Fixed, deliberately uneven per-element delays so the sign fails the same
   convincing way every time. The broken tubes go first, the payload last. */
const COPY_BEAT = 800          // let the user read "copied" before it starts dying
const LINE_DIE_DELAYS = [240, 700, 80, 940, 300, 560]
const ELEMENT_DIE_SPAN = 1360  // last line delay (940) + its die duration (420)
const COLLAPSE_DELAY = ELEMENT_DIE_SPAN + 40 // keep in sync with the CSS animation-delay on --dying
const COLLAPSE_DURATION = 520
const DEATH_TOTAL = COLLAPSE_DELAY + COLLAPSE_DURATION + 40
const DIM_DURATION = 900       // keep in sync with terminal-dim-down in the CSS
const DIM_TOTAL = Math.max(...LINE_DIE_DELAYS) + DIM_DURATION + 40

const dieDelay = (index: number) => `${LINE_DIE_DELAYS[index % LINE_DIE_DELAYS.length]}ms`

const prefersReducedMotion =
    typeof window !== 'undefined' && typeof window.matchMedia === 'function'
        ? window.matchMedia('(prefers-reduced-motion: reduce)').matches
        : false

const {
    lines: typedLines,
    finished,
    cursorIndex,
    start,
    stop
} = useTypewriter({ instant: prefersReducedMotion })

/* the frame element runs the power-on, so it is the one thing that knows when
   the tube is actually lit; the typewriter waits for it */
const frame = ref<HTMLElement | null>(null)

/* every timeout this component owns lives here, so one call cleans them all up */
let timers: number[] = []

const later = (fn: () => void, delay: number) => {
    timers.push(window.setTimeout(fn, delay))
}

/* --- waiting for the power-on -------------------------------------------
   Phase 1 lives entirely in CSS, so nothing here hardcodes its length: we
   listen for the animationend of the layer that IS the power-on, and the
   safety timer reads its duration off the element, which means the stylesheet
   stays the single source of truth and JS cannot drift away from it. */
const POWER_ON_ANIMATION = 'terminal-power-current'
const POWER_ON_FALLBACK = 2400 // only used if there is no element to ask
const POWER_ON_GRACE = 250     // the net fires just after the animation should have

/** true once this instance is fully lit, so a later run never waits again */
let poweredOn = false
let powerOnEl: HTMLElement | null = null
let powerOnHandler: ((event: AnimationEvent) => void) | null = null
/** bumped on every cancel, so work queued across a tick can tell it is stale */
let runToken = 0

const dropPowerOnWait = () => {
    if (powerOnEl && powerOnHandler) {
        powerOnEl.removeEventListener('animationend', powerOnHandler)
    }
    powerOnEl = null
    powerOnHandler = null
}

const clearPending = () => {
    timers.forEach((id) => window.clearTimeout(id))
    timers = []
    dropPowerOnWait()
    runToken++
}

const powerOnMs = (el: HTMLElement) => {
    const raw = window.getComputedStyle(el).animationDuration.split(',')[0].trim()
    const value = parseFloat(raw)
    if (Number.isNaN(value)) return POWER_ON_FALLBACK
    return raw.endsWith('ms') ? value : value * 1000
}

/** run `then` the moment the tube is fully on - never while it is still striking */
const afterPowerOn = (then: () => void) => {
    const el = frame.value
    if (poweredOn || !el) {
        poweredOn = true
        then()
        return
    }

    const fire = () => {
        if (!powerOnHandler) return // the event already won, or we were cancelled
        dropPowerOnWait()
        poweredOn = true
        then()
    }

    powerOnEl = el
    powerOnHandler = (event: AnimationEvent) => {
        // the bloom and electrode layers bubble their own end up to the frame
        if (event.animationName === POWER_ON_ANIMATION) fire()
    }
    el.addEventListener('animationend', powerOnHandler)
    later(fire, powerOnMs(el) + POWER_ON_GRACE)
}

const copied = ref(false)

const isGone = () => phase.value === 'dying' || phase.value === 'dead' || phase.value === 'dormant'

/** where the terminal ends up once it has died */
const settle = () => {
    phase.value = props.death === 'dim' ? 'dormant' : 'dead'
    if (phase.value === 'dead') emit('dead')
}

/**
 * Straight to the finished output, from wherever the intro is. It cancels the
 * pending "wait for the power-on" as well, so skipping while the tube is still
 * striking lands on the end state instead of typing everything out afterwards;
 * the terminal goes alive off the back of `finished`, which snaps the frame to
 * fully lit and drops the half-finished power-on layers.
 */
const skipIntro = () => {
    clearPending()
    if (!finished.value) start(props.lines, true)
}

/** shared by the copy button and the auto-death timer */
const die = () => {
    if (isGone()) return

    skipIntro() // always die from the complete picture, never mid-sentence

    if (prefersReducedMotion) {
        settle() // no dying animation, just the end state
        return
    }

    phase.value = 'dying'
    later(settle, props.death === 'dim' ? DIM_TOTAL : DEATH_TOTAL)
}

const scheduleAutoDeath = () => {
    if (props.autoDeath > 0) later(die, props.autoDeath)
}

const copyLink = async () => {
    if (isGone()) return

    const text = props.copyText
    if (text) {
        try {
            await navigator.clipboard.writeText(text)
        } catch {
            /* clipboard can be blocked; the text stays selectable in the terminal */
        }
    }

    emit('copy')
    /* a manual copy cancels any pending auto-death - and the wait for the
       power-on with it, so if the tube was still striking, show what is being
       copied now instead of dying with an empty box */
    const stillDark = !poweredOn
    clearPending()
    if (stillDark) skipIntro()
    copied.value = true
    later(die, COPY_BEAT)
}

/**
 * Bring a dormant terminal back to full strength (dim death only).
 * The collapse death unmounts instead, so there the parent re-mounts us.
 */
const relight = () => {
    if (phase.value !== 'dormant') return
    clearPending()
    copied.value = false
    phase.value = 'alive'
    scheduleAutoDeath()
}

defineExpose({ relight })

watch(finished, (value) => {
    if (!value) return
    emit('done')
    if (phase.value === 'typing') {
        phase.value = 'alive' // phase 2 hands over to phase 3
        scheduleAutoDeath()
    }
})

watch(() => props.lines, (value) => {
    clearPending()
    copied.value = false
    phase.value = 'typing'

    /* re-light and reduced motion both open on the finished picture: there is
       no power-on to sit through, so nothing is gated */
    if (prefersReducedMotion || props.instant) {
        poweredOn = true
        start(value, true)
        return
    }

    /* the body stays empty until the tube is lit - text on a struggling tube
       reads wrong. The frame only exists after this render, hence nextTick. */
    const token = runToken
    nextTick(() => {
        if (token !== runToken) return // a newer run took over in the meantime
        afterPowerOn(() => start(value, false))
    })
}, { immediate: true })

onBeforeUnmount(() => {
    stop()
    clearPending()
})
</script>

<style>
</style>
