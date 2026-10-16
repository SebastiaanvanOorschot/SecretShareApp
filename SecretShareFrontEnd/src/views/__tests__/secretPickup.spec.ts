import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import axios from 'axios'
import NeonTerminal from '../../components/NeonTerminal.vue'
import type { TerminalLine } from '../../composables/useTypewriter'
import SecretPickup from '../secretPickup.vue'

vi.mock('axios', () => ({
    default: { post: vi.fn() },
    Axios: class {},
    AxiosError: class {}
}))

vi.mock('vue-router', () => ({
    useRoute: () => ({ params: { hash: 'abc123' } })
}))

const API = 'https://api.test'

const post = vi.mocked(axios.post)
let alertSpy: ReturnType<typeof vi.fn>

const mountView = () =>
    mount(SecretPickup, {
        global: { stubs: { NeonTerminal: true } }
    })

type View = ReturnType<typeof mountView>

/* the "come a little closer.." link is the button people actually click */
const clickSubmit = async (view: View) => {
    await view.find('a.submitButton').trigger('click')
    await flushPromises()
}

const terminal = (view: View) => view.findComponent(NeonTerminal)

beforeEach(() => {
    vi.stubEnv('VITE_SECRETSHAREAPI_URL', API)
    post.mockReset()
    alertSpy = vi.fn()
    vi.stubGlobal('alert', alertSpy)
})

describe('secretPickup view', () => {
    it('does not post and shows "required" without a passphrase', async () => {
        const view = mountView()

        await clickSubmit(view)

        expect(post).not.toHaveBeenCalled()
        expect(view.findAll('i').filter((i) => i.text() === 'required')).toHaveLength(1)
    })

    it('posts the passphrase together with the url hash to the retrieve endpoint', async () => {
        post.mockResolvedValue({ data: 'my secret' })
        const view = mountView()
        await view.find('input[name="passphrase"]').setValue('my phrase')

        await clickSubmit(view)

        expect(post).toHaveBeenCalledTimes(1)
        expect(post).toHaveBeenCalledWith(`${API}/api/secret/retrieve`, {
            passphrase: 'my phrase',
            urlExtension: 'abc123'
        })
    })

    it('shows the retrieved secret as the scrambled line that survives the terminal dying', async () => {
        post.mockResolvedValue({ data: 'my secret' })
        const view = mountView()
        await view.find('input[name="passphrase"]').setValue('my phrase')

        await clickSubmit(view)

        expect(terminal(view).exists()).toBe(true)
        expect(terminal(view).props('copyText')).toBe('my secret')
        expect(terminal(view).props('death')).toBe('dim')
        const lines = terminal(view).props('lines') as TerminalLine[]
        expect(lines).toContainEqual({ text: 'my secret', reveal: 'scramble', keep: true })
    })

    it('shows no terminal before a secret is retrieved', () => {
        const view = mountView()

        expect(terminal(view).exists()).toBe(false)
    })

    it('alerts the error and shows no secret when retrieval fails', async () => {
        // wrong passphrase, already retrieved and expired all come back as a 404
        post.mockRejectedValue(new Error('Request failed with status code 404'))
        const view = mountView()
        await view.find('input[name="passphrase"]').setValue('wrong phrase')

        await clickSubmit(view)

        expect(alertSpy).toHaveBeenCalledWith('Request failed with status code 404')
        expect(terminal(view).exists()).toBe(false)
    })
})
