/**
 * Decrypt-scramble: instead of typing left to right, the whole line shows
 * encrypted-looking noise straight away and then locks onto the real characters
 * one by one, as if the encryption is being undone in front of you.
 *
 * Pure helpers — the timer that drives them lives in useTypewriter, so a line
 * that scrambles is sequenced by exactly the same (single) timer as a line that types.
 */

const GLYPHS = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*<>/\[]{}=+~?'

/** ms between two scramble frames */
export const SCRAMBLE_FRAME_DELAY = 40
/** how many frames of noise pass before the next character(s) lock in */
export const SCRAMBLE_FRAMES_PER_LOCK = 2
/** cap on the number of lock steps, so a long secret does not take forever */
export const SCRAMBLE_MAX_LOCKS = 36

const randomGlyph = () => GLYPHS.charAt(Math.floor(Math.random() * GLYPHS.length))

/** how many characters have locked in after `frame` frames */
export const lockedCount = (text: string, frame: number) => {
    const perLock = Math.max(1, Math.ceil(text.length / SCRAMBLE_MAX_LOCKS))
    return Math.min(text.length, Math.floor(frame / SCRAMBLE_FRAMES_PER_LOCK) * perLock)
}

/** the first `locked` characters are real, everything after them is fresh noise */
export const scrambleMask = (text: string, locked: number) => {
    let out = text.slice(0, locked)
    for (let i = locked; i < text.length; i++) out += randomGlyph()
    return out
}
