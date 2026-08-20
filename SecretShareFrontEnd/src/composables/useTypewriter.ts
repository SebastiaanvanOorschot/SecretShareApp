import { onScopeDispose, ref } from 'vue'
import {
    SCRAMBLE_FRAME_DELAY,
    lockedCount,
    scrambleMask
} from './decryptScramble'

/** One line of terminal output. `href` turns the line into a link once it is fully typed. */
export interface TerminalLine {
    text: string
    href?: string
    /** marks this line as a "broken tube": it flickers off-beat once the terminal is alive */
    flicker?: boolean
    /** how the line reveals itself: typed left to right (default) or decrypt-scrambled */
    reveal?: 'type' | 'scramble'
    /** this line survives the death, dimmed, instead of going out (death="dim") */
    keep?: boolean
}

/** A line as it is being rendered: `typed` grows until it equals `text`. */
export interface TypedLine extends TerminalLine {
    typed: string
    complete: boolean
}

export interface TypewriterOptions {
    /** ms between two characters */
    charDelay?: number
    /** ms pause between two lines */
    lineDelay?: number
    /** ms before the first character appears */
    startDelay?: number
    /** render everything at once (prefers-reduced-motion) */
    instant?: boolean
}

/**
 * Types an array of lines out character by character.
 * Only one timer is ever alive; calling start() again cancels the previous run.
 */
export function useTypewriter(options: TypewriterOptions = {}) {
    const charDelay = options.charDelay ?? 24
    const lineDelay = options.lineDelay ?? 240
    const startDelay = options.startDelay ?? 550

    const lines = ref<TypedLine[]>([])
    const finished = ref(false)
    const cursorIndex = ref(-1)

    let timer: number | undefined
    let source: TerminalLine[] = []
    let lineIndex = 0
    let charIndex = 0
    let scrambleFrame = 0

    const clearTimer = () => {
        if (timer !== undefined) {
            window.clearTimeout(timer)
            timer = undefined
        }
    }

    const schedule = (fn: () => void, delay: number) => {
        clearTimer()
        timer = window.setTimeout(fn, delay)
    }

    const finish = () => {
        clearTimer()
        lines.value = source.map((line) => ({ ...line, typed: line.text, complete: true }))
        cursorIndex.value = source.length - 1
        finished.value = true
    }

    const pushLine = (index: number) => {
        const line = source[index]
        lines.value.push({
            ...line,
            // a scrambling line is fully garbled from its very first frame
            typed: line.reveal === 'scramble' ? scrambleMask(line.text, 0) : '',
            complete: false
        })
        cursorIndex.value = index
    }

    const advanceLine = () => {
        lines.value[lineIndex].typed = source[lineIndex].text
        lines.value[lineIndex].complete = true
        lineIndex++
        charIndex = 0
        scrambleFrame = 0

        if (lineIndex >= source.length) {
            finish()
            return
        }

        pushLine(lineIndex)
        schedule(step, lineDelay)
    }

    const step = () => {
        const current = source[lineIndex]

        if (current.reveal === 'scramble') {
            const locked = lockedCount(current.text, scrambleFrame)
            if (locked >= current.text.length) {
                advanceLine()
                return
            }
            lines.value[lineIndex].typed = scrambleMask(current.text, locked)
            scrambleFrame++
            schedule(step, SCRAMBLE_FRAME_DELAY)
            return
        }

        if (charIndex < current.text.length) {
            charIndex++
            lines.value[lineIndex].typed = current.text.slice(0, charIndex)
            schedule(step, charDelay)
            return
        }

        advanceLine()
    }

    /** (Re)start the typewriter with a fresh set of lines; `instant` jumps straight to the end state. */
    const start = (input: TerminalLine[], instant = options.instant) => {
        clearTimer()
        source = input.map((line) => ({ ...line }))
        lines.value = []
        cursorIndex.value = -1
        finished.value = false
        lineIndex = 0
        charIndex = 0
        scrambleFrame = 0

        if (!source.length) {
            finished.value = true
            return
        }

        if (instant) {
            finish()
            return
        }

        pushLine(0)
        schedule(step, startDelay)
    }

    /** Jump straight to the fully typed end state. */
    const skip = () => {
        if (!finished.value) finish()
    }

    /** Stop without completing (used on unmount). */
    const stop = () => clearTimer()

    onScopeDispose(clearTimer)

    return { lines, finished, cursorIndex, start, skip, stop }
}
