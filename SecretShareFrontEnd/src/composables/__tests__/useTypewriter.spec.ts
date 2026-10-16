import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { effectScope, type EffectScope } from 'vue'
import { SCRAMBLE_FRAME_DELAY } from '../decryptScramble'
import { useTypewriter, type TerminalLine, type TypewriterOptions } from '../useTypewriter'

/* small, distinct delays so every step in a test is easy to point at */
const START = 50
const CHAR = 10
const LINE = 100

let scope: EffectScope

const setup = (options: TypewriterOptions = {}) => {
    scope = effectScope()
    return scope.run(() =>
        useTypewriter({ startDelay: START, charDelay: CHAR, lineDelay: LINE, ...options })
    )!
}

const typedTexts = (typewriter: ReturnType<typeof setup>) =>
    typewriter.lines.value.map((line) => line.typed)

beforeEach(() => {
    vi.useFakeTimers()
})

afterEach(() => {
    scope?.stop()
    vi.useRealTimers()
})

describe('useTypewriter', () => {
    it('shows the first line empty until the start delay has passed', () => {
        const typewriter = setup()

        typewriter.start([{ text: 'ab' }])

        expect(typedTexts(typewriter)).toEqual([''])
        expect(typewriter.cursorIndex.value).toBe(0)
        vi.advanceTimersByTime(START - 1)
        expect(typedTexts(typewriter)).toEqual([''])
    })

    it('types one character per char delay, then moves to the next line after the line delay', () => {
        const typewriter = setup()
        typewriter.start([{ text: 'ab' }, { text: 'cd' }])

        vi.advanceTimersByTime(START)
        expect(typedTexts(typewriter)).toEqual(['a'])

        vi.advanceTimersByTime(CHAR)
        expect(typedTexts(typewriter)).toEqual(['ab'])
        expect(typewriter.lines.value[0].complete).toBe(false)

        // one more tick completes the line and pushes the next one, still empty
        vi.advanceTimersByTime(CHAR)
        expect(typedTexts(typewriter)).toEqual(['ab', ''])
        expect(typewriter.lines.value[0].complete).toBe(true)
        expect(typewriter.cursorIndex.value).toBe(1)

        vi.advanceTimersByTime(LINE - 1)
        expect(typedTexts(typewriter)).toEqual(['ab', ''])
        vi.advanceTimersByTime(1)
        expect(typedTexts(typewriter)).toEqual(['ab', 'c'])
    })

    it('finishes once the last line is typed', () => {
        const typewriter = setup()
        typewriter.start([{ text: 'ab' }, { text: 'cd' }])

        vi.runAllTimers()

        expect(typewriter.finished.value).toBe(true)
        expect(typedTexts(typewriter)).toEqual(['ab', 'cd'])
        expect(typewriter.lines.value.every((line) => line.complete)).toBe(true)
        expect(typewriter.cursorIndex.value).toBe(1)
        expect(vi.getTimerCount()).toBe(0)
    })

    it('keeps the extra line fields such as href and flicker', () => {
        const typewriter = setup()
        const lines: TerminalLine[] = [{ text: 'link', href: 'https://example.test', flicker: true }]

        typewriter.start(lines)
        vi.runAllTimers()

        expect(typewriter.lines.value[0]).toMatchObject({
            text: 'link',
            href: 'https://example.test',
            flicker: true,
            typed: 'link',
            complete: true
        })
    })

    it('does not change while running when the input array is mutated afterwards', () => {
        const typewriter = setup()
        const lines: TerminalLine[] = [{ text: 'ab' }]

        typewriter.start(lines)
        lines[0].text = 'changed'
        vi.runAllTimers()

        expect(typedTexts(typewriter)).toEqual(['ab'])
    })

    describe('scramble reveal', () => {
        it('starts fully garbled and locks onto the real text', () => {
            vi.spyOn(Math, 'random').mockReturnValue(0) // noise is always 'A'
            const typewriter = setup()

            typewriter.start([{ text: 'hey', reveal: 'scramble' }])
            expect(typedTexts(typewriter)).toEqual(['AAA'])

            // frames 0 and 1 are still pure noise, frame 2 locks the first character
            vi.advanceTimersByTime(START + 2 * SCRAMBLE_FRAME_DELAY)
            expect(typedTexts(typewriter)).toEqual(['hAA'])

            vi.runAllTimers()
            expect(typedTexts(typewriter)).toEqual(['hey'])
            expect(typewriter.finished.value).toBe(true)
        })

        it('keeps the garbled line as long as the real text', () => {
            const typewriter = setup()

            typewriter.start([{ text: 'my secret', reveal: 'scramble' }])

            expect(typewriter.lines.value[0].typed).toHaveLength('my secret'.length)
            expect(typewriter.lines.value[0].typed).not.toBe('my secret')
        })
    })

    describe('skip, stop and restart', () => {
        it('skip jumps straight to the fully typed end state', () => {
            const typewriter = setup()
            typewriter.start([{ text: 'ab' }, { text: 'cd', reveal: 'scramble' }])
            vi.advanceTimersByTime(START)

            typewriter.skip()

            expect(typewriter.finished.value).toBe(true)
            expect(typedTexts(typewriter)).toEqual(['ab', 'cd'])
            expect(vi.getTimerCount()).toBe(0)
        })

        it('stop halts without completing', () => {
            const typewriter = setup()
            typewriter.start([{ text: 'ab' }])
            vi.advanceTimersByTime(START)

            typewriter.stop()
            vi.runAllTimers()

            expect(typedTexts(typewriter)).toEqual(['a'])
            expect(typewriter.finished.value).toBe(false)
        })

        it('start cancels the previous run so only one timer is ever alive', () => {
            const typewriter = setup()
            typewriter.start([{ text: 'first run' }])
            vi.advanceTimersByTime(START + CHAR)

            typewriter.start([{ text: 'xy' }])

            expect(vi.getTimerCount()).toBe(1)
            expect(typedTexts(typewriter)).toEqual([''])
            expect(typewriter.finished.value).toBe(false)

            vi.runAllTimers()
            expect(typedTexts(typewriter)).toEqual(['xy'])
        })

        it('start can run again after a finished run', () => {
            const typewriter = setup()
            typewriter.start([{ text: 'ab' }])
            vi.runAllTimers()

            typewriter.start([{ text: 'cd' }])

            expect(typewriter.finished.value).toBe(false)
            vi.runAllTimers()
            expect(typedTexts(typewriter)).toEqual(['cd'])
        })
    })

    describe('instant and empty input', () => {
        it('start with instant renders everything at once', () => {
            const typewriter = setup()

            typewriter.start([{ text: 'ab' }, { text: 'cd' }], true)

            expect(typewriter.finished.value).toBe(true)
            expect(typedTexts(typewriter)).toEqual(['ab', 'cd'])
            expect(vi.getTimerCount()).toBe(0)
        })

        it('the instant option is the default for every start', () => {
            const typewriter = setup({ instant: true })

            typewriter.start([{ text: 'ab' }])

            expect(typewriter.finished.value).toBe(true)
            expect(typedTexts(typewriter)).toEqual(['ab'])
        })

        it('finishes immediately without lines', () => {
            const typewriter = setup()

            typewriter.start([])

            expect(typewriter.finished.value).toBe(true)
            expect(typewriter.lines.value).toEqual([])
            expect(typewriter.cursorIndex.value).toBe(-1)
            expect(vi.getTimerCount()).toBe(0)
        })
    })

    it('clears its timer when the owning scope is disposed', () => {
        const typewriter = setup()
        typewriter.start([{ text: 'ab' }])
        expect(vi.getTimerCount()).toBe(1)

        scope.stop()

        expect(vi.getTimerCount()).toBe(0)
        vi.runAllTimers()
        expect(typedTexts(typewriter)).toEqual([''])
    })
})
