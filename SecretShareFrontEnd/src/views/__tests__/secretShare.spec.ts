import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import axios from 'axios'
import NeonTerminal from '../../components/NeonTerminal.vue'
import type { TerminalLine } from '../../composables/useTypewriter'
import SecretShare from '../secretShare.vue'

vi.mock('axios', () => ({
    default: { post: vi.fn() },
    Axios: class {},
    AxiosError: class {}
}))

const API = 'https://api.test'
const PICKUP_URL = 'https://pickup.test/secretPickup/abc123'

const post = vi.mocked(axios.post)
let writeText: ReturnType<typeof vi.fn>
let alertSpy: ReturnType<typeof vi.fn>

const mountView = () =>
    mount(SecretShare, {
        global: { stubs: { NeonTerminal: true } }
    })

type View = ReturnType<typeof mountView>

const fillForm = async (view: View, lifetime = '3600') => {
    await view.find('input[name="secret"]').setValue('my secret')
    await view.find('input[name="passphrase"]').setValue('my phrase')
    await view.find(`input[name="lifetime"][value="${lifetime}"]`).setValue()
}

/* the "Tell someone else!" link is the button people actually click */
const clickSubmit = async (view: View) => {
    await view.find('a.submitButton').trigger('click')
    await flushPromises()
}

const terminal = (view: View) => view.findComponent(NeonTerminal)

beforeEach(() => {
    vi.stubEnv('VITE_SECRETSHAREAPI_URL', API)
    post.mockReset()
    writeText = vi.fn().mockResolvedValue(undefined)
    vi.stubGlobal('navigator', { ...navigator, clipboard: { writeText } })
    alertSpy = vi.fn()
    vi.stubGlobal('alert', alertSpy)
})

describe('secretShare view', () => {
    it('does not post and shows "required" for each empty field', async () => {
        const view = mountView()

        await clickSubmit(view)

        expect(post).not.toHaveBeenCalled()
        expect(view.findAll('i').filter((i) => i.text() === 'required')).toHaveLength(3)
    })

    it('does not post while one field is still missing', async () => {
        const view = mountView()
        await view.find('input[name="secret"]').setValue('my secret')
        await view.find('input[name="passphrase"]').setValue('my phrase')

        await clickSubmit(view)

        expect(post).not.toHaveBeenCalled()
    })

    it('posts the form to the store endpoint', async () => {
        post.mockResolvedValue({ data: PICKUP_URL })
        const view = mountView()
        await fillForm(view)

        await clickSubmit(view)

        expect(post).toHaveBeenCalledTimes(1)
        expect(post).toHaveBeenCalledWith(`${API}/api/secret/store`, {
            secret: 'my secret',
            lifetime: '3600',
            passphrase: 'my phrase'
        })
    })

    it('also submits when the form itself is submitted (Enter in a field)', async () => {
        post.mockResolvedValue({ data: PICKUP_URL })
        const view = mountView()
        await fillForm(view)

        await view.find('form').trigger('submit')
        await flushPromises()

        expect(post).toHaveBeenCalledTimes(1)
    })

    it('copies the pickup url to the clipboard and shows it in the terminal', async () => {
        post.mockResolvedValue({ data: PICKUP_URL })
        const view = mountView()
        await fillForm(view, '86400')

        await clickSubmit(view)

        expect(writeText).toHaveBeenCalledWith(PICKUP_URL)
        expect(terminal(view).exists()).toBe(true)
        expect(terminal(view).props('copyText')).toBe(PICKUP_URL)
        const lines = terminal(view).props('lines') as TerminalLine[]
        expect(lines).toContainEqual({ text: PICKUP_URL, href: PICKUP_URL })
        expect(lines).toContainEqual({ text: 'expires in 1 day' })
    })

    it('shows no terminal before a secret is stored', () => {
        const view = mountView()

        expect(terminal(view).exists()).toBe(false)
    })

    it('alerts the error and shows no terminal when storing fails', async () => {
        post.mockRejectedValue(new Error('Network Error'))
        const view = mountView()
        await fillForm(view)

        await clickSubmit(view)

        expect(alertSpy).toHaveBeenCalledWith('Network Error')
        expect(writeText).not.toHaveBeenCalled()
        expect(terminal(view).exists()).toBe(false)
    })
})
