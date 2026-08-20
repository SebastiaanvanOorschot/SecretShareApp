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
        <span class="neon-terminal__frame" aria-hidden="true"></span>

        <div class="neon-terminal__bar">
            <button
                v-if="!prefersReducedMotion"
                type="button"
                class="neon-terminal__btn neon-terminal__btn--skip"
                :class="{ 'neon-terminal__btn--faulty': isAlive }"
                :disabled="finished"
                @click="skip"
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
import { computed, onBeforeUnmount, ref, watch } from 'vue'
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
 * typing  -> phase 1+2: frame powers on, output reveals itself
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
    skip,
    start,
    stop
} = useTypewriter({ instant: prefersReducedMotion })

/* every timeout this component owns lives here, so one call cleans them all up */
let timers: number[] = []

const later = (fn: () => void, delay: number) => {
    timers.push(window.setTimeout(fn, delay))
}

const clearTimers = () => {
    timers.forEach((id) => window.clearTimeout(id))
    timers = []
}

const copied = ref(false)

const isGone = () => phase.value === 'dying' || phase.value === 'dead' || phase.value === 'dormant'

/** where the terminal ends up once it has died */
const settle = () => {
    phase.value = props.death === 'dim' ? 'dormant' : 'dead'
    if (phase.value === 'dead') emit('dead')
}

/** shared by the copy button and the auto-death timer */
const die = () => {
    if (isGone()) return

    clearTimers()
    skip() // always die from the complete picture, never mid-sentence

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
    clearTimers() // a manual copy cancels any pending auto-death
    copied.value = true
    later(die, COPY_BEAT)
}

/**
 * Bring a dormant terminal back to full strength (dim death only).
 * The collapse death unmounts instead, so there the parent re-mounts us.
 */
const relight = () => {
    if (phase.value !== 'dormant') return
    clearTimers()
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
    clearTimers()
    copied.value = false
    phase.value = 'typing'
    start(value, prefersReducedMotion || props.instant)
}, { immediate: true })

onBeforeUnmount(() => {
    stop()
    clearTimers()
})
</script>

<style>
</style>
