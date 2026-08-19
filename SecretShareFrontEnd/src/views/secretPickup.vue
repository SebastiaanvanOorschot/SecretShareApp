<template>
    <body>
        <form @submit.prevent="submitForm">
            <div class="column">
                <input name="urlExtension" v-model="formData.urlExtension" type="hidden" />                

                <button class="row help-btn" type="button"><span class='glowing-txt'>H<span class='faulty-letter'>E</span>LP ME</span></button>

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
                <div class="column alertContainerRetrieve">
                    <div ref="fadeIn" class="fadeIn">
                        <p ref="revealed" class="flicker">{{ secret }}</p>
                    </div>
                    <p ref="copied" class="copied">(COPIED TO CLIPBOARD)</p>
                    <p ref="noTell" class="noTell">DON'T TELL ANYONE</p>                
                </div>
            </div>
        </form>
    </body>
</template>    
 
<script setup lang="ts">
import { reactive, ref } from 'vue'; 
import { useVuelidate } from '@vuelidate/core';
import { required } from '@vuelidate/validators';     
import axios, { Axios, AxiosError } from 'axios';
import { useRoute } from 'vue-router';

const route = useRoute();
const secret = ref("");
const revealed = ref();
const noTell = ref();
const copied = ref();
const fadeIn = ref();

const formData = reactive({
    passphrase: "",
    urlExtension: route.params.hash
});

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
                fadeIn.value.style.animationPlayState = "running";
                revealed.value.style.animationPlayState = "running";
                copied.value.style.animationPlayState = "running";
                noTell.value.style.animationPlayState = "running";
                navigator.clipboard.writeText(returnData.data);
            })
            .catch((e: AxiosError) => {
                alert(e.message)
            });
    }    
};
    
</script>

<style>
</style>