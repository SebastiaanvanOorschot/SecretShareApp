<template>
    <body>
        <form @submit.prevent="submitForm">
            <div class="column">
                <button class="row help-btn" type="button"><span class='glowing-txt'>H<span class='faulty-letter'>E</span>LP ME</span></button>

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
                
                <div class="column lifetimeContainer" data-text="Decide how long we should keep the secret in our database" :style="{ border: currentBorder, 'box-shadow': currentBoxShadow }">                    
                    <label id="lifetime" :style="{ border: currentBorder, color: currentColor, 'box-shadow': currentBoxShadow }">Lifetime</label>
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
                    <a href="#" type="submit" class="row submitButton" v-on:click="submitForm" style="--clr:#00dfc4">
                        <span>Tell someone else!</span><i></i>
                    </a>                 
                </div>
                
                <div class="column alertContainer" ref="alertContainer" :style="{ opacity: 0 }">
                    <p ref="alertMessage" class="flicker">CHECK YOUR C<span class='faulty-letter'>L</span>IPBOARD</p>
                </div>   
            </div>
            
        </form>
    </body>
</template>

<script setup lang="ts">

import { reactive, ref, watch, computed } from 'vue';
import { useVuelidate } from '@vuelidate/core';
import { required } from '@vuelidate/validators';
import axios, { Axios, AxiosError } from 'axios';

const url = ref("");
const alertContainer = ref();
const currentBorder = ref('1px solid rgba(255, 255, 255, 0.493)');
const currentColor = ref('rgba(255, 255, 255, 0.493)');
const currentBoxShadow = ref('0 0 0px var(--white)');
const alertMessage = ref();

const formData = reactive({
    secret: "",
    lifetime: "",
    passphrase: ""
});

watch(() => formData.lifetime, (newValue) =>{
    if (newValue){
        currentBorder.value = '1px solid #00DFC4';
        currentColor.value = '#00DFC4';
        currentBoxShadow.value = '0 0 5px #fff';
    }
});

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
                alertContainer.value.style.opacity = 1;
                alertMessage.value.style.animationPlayState = "running";
            })
            .catch((e: AxiosError) => {
                alert(e.message)
            })        
    } 
};

</script>

<style>
</style>
