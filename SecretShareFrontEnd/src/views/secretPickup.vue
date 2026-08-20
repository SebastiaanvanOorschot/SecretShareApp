<template>
    <form @submit.prevent="submitForm">
            <div class="column">
                <input name="urlExtension" v-model="formData.urlExtension" type="hidden" />

                <button class="row help-btn" type="button" @mouseenter="relight" @focus="relight"><span class='glowing-txt'>H<span class='faulty-letter'>E</span>LP ME</span></button>

                <div class="row inputBox retrieveInfoText" data-text="Somebody wants to let you in on a secret.. They've probably given you a passphrase. Go and check it out. Be sure to store it safely though. Once retrieved it will be deleted permanently">
                </div>

                <div class="row inputBox retrievePassphraseContainer" data-text="Enter your passphrase">
                    <input type="text" name="passphrase" v-model="formData.passphrase" placeholder="" onfocus="placeholder=''">
                    <span>Passphrase</span>
                    <i v-for="error in v$.passphrase.$errors" :key="error.$uid">required</i>
                </div>

                <div class="column retrieveSubmitContainer" data-text="Click this button to submit your passphrase and retrieve the secret if it was correct.">
                    <a href="#" type="submit" v-on:click="submitForm" class="row submitButton" style="--clr:var(--accent)"><span>come a little closer..</span><i></i></a>
                </div>

                <NeonTerminal
                    v-if="secret"
                    ref="terminal"
                    class="terminalContainerRetrieve"
                    :lines="terminalLines"
                    :copy-text="secret"
                    death="dim"
                    :auto-death="AUTO_DIM_DELAY"
                />
            </div>
    </form>
</template>

<script setup lang="ts">
import { computed, ref, reactive } from 'vue';
import { useVuelidate } from '@vuelidate/core';
import { required } from '@vuelidate/validators';
import axios, { Axios, AxiosError } from 'axios';
import { useRoute } from 'vue-router';
import NeonTerminal from '../components/NeonTerminal.vue';
import type { TerminalLine } from '../composables/useTypewriter';

const route = useRoute();

/* The secret lives here and nowhere else: plain in-memory component state.
   No localStorage, no sessionStorage, no cookies - it is gone on refresh,
   which matches the server having destroyed it on retrieval. */
const secret = ref("");
const terminal = ref();

/* ms the fully revealed secret stays at full strength before it dims by itself */
const AUTO_DIM_DELAY = 5000;

const formData = reactive({
    passphrase: "",
    urlExtension: route.params.hash
});

/* terminal output; the secret line decrypt-scrambles and is the one that
   stays behind (dimmed) when the rest of the terminal goes out */
const terminalLines = computed<TerminalLine[]>(() => [
    { text: "verifying passphrase..." },
    { text: "decrypting..." },
    { text: "secret retrieved" },
    { text: secret.value, reveal: 'scramble', keep: true },
    { text: "this secret has now been destroyed", flicker: true }
]);

/* re-light: hovering (or focusing) the lamp wakes a dimmed terminal back up.
   The terminal is still mounted here, so it re-lights itself instead of
   being re-mounted the way the share page does it. */
const relight = () => {
    terminal.value?.relight();
};

const rules = {
    passphrase: { required }
};

const v$ = useVuelidate(rules, formData);

const submitForm = async () => {
    const valid = await v$.value.$validate();
    if (valid) {
        await axios.post(import.meta.env.VITE_SECRETSHAREAPI_URL + '/api/secret/retrieve', formData)
            .then(returnData => {
                secret.value = returnData.data;
            })
            .catch((e: AxiosError) => {
                alert(e.message)
            });
    }
};

</script>

<style>
</style>
