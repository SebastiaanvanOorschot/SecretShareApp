<template>
    <form @submit.prevent="submitForm">
            <div class="column">
                <button class="row help-btn" type="button" @mouseenter="relight" @focus="relight"><span class='glowing-txt'>H<span class='faulty-letter'>E</span>LP ME</span></button>

                <div  class="row inputBox secretContainer" data-text="Fill in a secret, don't worry, we will encrypt it and store it in our database">
                    <input type="text" name="secret" class="inputBox" v-model="formData.secret" placeholder="" onfocus="placeholder=''">
                    <span>Secret</span>
                    <i v-for="error in v$.secret.$errors" :key="error.$uid">required</i>
                </div>

                <div  class="row inputBox passphraseContainer" data-text="Think of a passphrase. This is the key needed to retrieve a stored secret">
                    <input type="text" name="passphrase" v-model="formData.passphrase" placeholder="" onfocus="placeholder=''">
                    <span>Passphrase</span>
                    <i v-for="error in v$.passphrase.$errors" :key="error.$uid">required</i>
                </div>
                
                <div class="column lifetimeContainer" data-text="Decide how long we should keep the secret in our database">                    
                    <label id="lifetime">Lifetime</label>
                    <div class="row radioGroup">
                        <div class="column radio">
                            <input type="radio" name="lifetime" v-model="formData.lifetime" value="3600" /> 
                            <span>1 hour</span>
                        </div>
                        <div class="column radio">
                            <input type="radio" name="lifetime" v-model="formData.lifetime" value="86400" />
                            <span>1 day</span>
                        </div>
                        <div class="column radio">
                            <input type="radio" name="lifetime" v-model="formData.lifetime" value="259200" />
                            <span>3 days</span>
                        </div>                
                    </div> 
                    <i v-for="error in v$.lifetime.$errors" :key="error.$uid">required</i>                     
                </div>                
                
                <div class="column submitContainer" data-text="Click this button to store the secret and copy the retrieval link to your clipboard">                    
                    <a href="#" type="submit" class="row submitButton" v-on:click="submitForm" style="--clr:var(--accent)">
                        <span>Tell someone else!</span><i></i>
                    </a>                 
                </div>
                
                <NeonTerminal
                    v-if="terminalVisible"
                    :key="terminalRun"
                    class="terminalContainer"
                    :lines="terminalLines"
                    :copy-text="url"
                    :instant="relit"
                    @dead="onTerminalDead"
                />
            </div>
            
    </form>
</template>

<script setup lang="ts">

import { reactive, ref, computed } from 'vue';
import { useVuelidate } from '@vuelidate/core';
import { required } from '@vuelidate/validators';
import axios, { Axios, AxiosError } from 'axios';
import NeonTerminal from '../components/NeonTerminal.vue';
import type { TerminalLine } from '../composables/useTypewriter';

const url = ref("");
const terminalVisible = ref(false);
/* true once the terminal has died at least once: re-lighting opens it straight
   in the finished, living state instead of typing everything out again */
const relit = ref(false);
/* forces a fresh NeonTerminal instance per run, so no state leaks between lives */
const terminalRun = ref(0);

const formData = reactive({
    secret: "",
    lifetime: "",
    passphrase: ""
});

/* lifetime is stored as seconds; the terminal shows the human label the user picked */
const lifetimeLabels: Record<string, string> = {
    "3600": "1 hour",
    "86400": "1 day",
    "259200": "3 days"
};

const lifetimeLabel = computed(() => lifetimeLabels[formData.lifetime] ?? "a while");

/* terminal output, kept as data so extra lines are a one-liner to add */
const terminalLines = computed<TerminalLine[]>(() => [
    { text: "encrypting secret..." },
    { text: "secret secured" },
    { text: "link copied to clipboard", flicker: true },
    { text: url.value, href: url.value },
    { text: `expires in ${lifetimeLabel.value}` },
    { text: "go tell someone else!" }
]);

/* phase 4 finished: the terminal is gone, but url/lines are kept so the lamp can bring it back */
const onTerminalDead = () => {
    terminalVisible.value = false;
    relit.value = true;
};

/* re-light: hovering (or focusing) the lamp powers a dead terminal back up,
   straight into the finished + living state */
const relight = () => {
    if (!url.value || terminalVisible.value) return;
    terminalRun.value++;
    terminalVisible.value = true;
};

const rules = {
    secret: { required },
    lifetime: { required },
    passphrase: { required }
};

const v$ = useVuelidate(rules, formData);

const submitForm = async () => {
    const valid = await v$.value.$validate();
    if (valid) {         
        await axios.post(import.meta.env.VITE_SECRETSHAREAPI_URL +'/api/secret/store', formData)
            .then(returnData => {
                url.value = returnData.data;
                navigator.clipboard.writeText(returnData.data);
                relit.value = false;
                terminalRun.value++;
                terminalVisible.value = true;
            })
            .catch((e: AxiosError) => {
                alert(e.message)
            })        
    } 
};

</script>

<style>
</style>
