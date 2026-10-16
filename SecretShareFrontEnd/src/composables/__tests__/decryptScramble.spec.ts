import { describe, expect, it, vi } from 'vitest'
import {
    SCRAMBLE_FRAMES_PER_LOCK,
    SCRAMBLE_MAX_LOCKS,
    lockedCount,
    scrambleMask
} from '../decryptScramble'

/* the noise alphabet from decryptScramble, as a character class */
const NOISE = /^[A-Za-z0-9!@#$%^&*<>/[\]{}=+~?]*$/

describe('lockedCount', () => {
    it('locks one character every SCRAMBLE_FRAMES_PER_LOCK frames for a short text', () => {
        expect(lockedCount('hello', 0)).toBe(0)
        expect(lockedCount('hello', SCRAMBLE_FRAMES_PER_LOCK - 1)).toBe(0)
        expect(lockedCount('hello', SCRAMBLE_FRAMES_PER_LOCK)).toBe(1)
        expect(lockedCount('hello', SCRAMBLE_FRAMES_PER_LOCK * 4)).toBe(4)
    })

    it('never locks more characters than the text has', () => {
        expect(lockedCount('hello', SCRAMBLE_FRAMES_PER_LOCK * 5)).toBe(5)
        expect(lockedCount('hello', 1000)).toBe(5)
    })

    it('locks several characters per step once the text is longer than SCRAMBLE_MAX_LOCKS', () => {
        const text = 'x'.repeat(SCRAMBLE_MAX_LOCKS * 2)

        expect(lockedCount(text, SCRAMBLE_FRAMES_PER_LOCK)).toBe(2)
    })

    it.each([1, 10, SCRAMBLE_MAX_LOCKS, 100, 5000])(
        'finishes a %i character text within SCRAMBLE_MAX_LOCKS lock steps',
        (length) => {
            const text = 'x'.repeat(length)
            const lastFrame = SCRAMBLE_MAX_LOCKS * SCRAMBLE_FRAMES_PER_LOCK

            expect(lockedCount(text, lastFrame)).toBe(length)
        }
    )

    it('returns 0 for an empty text', () => {
        expect(lockedCount('', 0)).toBe(0)
        expect(lockedCount('', 100)).toBe(0)
    })
})

describe('scrambleMask', () => {
    it('keeps the locked characters and replaces the rest with noise', () => {
        vi.spyOn(Math, 'random').mockReturnValue(0) // always the first glyph, 'A'

        expect(scrambleMask('hello', 2)).toBe('heAAA')
    })

    it('keeps the length of the text', () => {
        expect(scrambleMask('my secret', 0)).toHaveLength(9)
        expect(scrambleMask('my secret', 4)).toHaveLength(9)
    })

    it('only uses noise glyphs for the unlocked part', () => {
        const noise = scrambleMask('a'.repeat(500), 0)

        expect(noise).toMatch(NOISE)
    })

    it('can pick the last glyph without running past the alphabet', () => {
        vi.spyOn(Math, 'random').mockReturnValue(0.9999999)

        expect(scrambleMask('abc', 0)).toBe('???')
    })

    it('returns the plain text once everything is locked', () => {
        expect(scrambleMask('hello', 5)).toBe('hello')
    })

    it('handles an empty text', () => {
        expect(scrambleMask('', 0)).toBe('')
    })
})
