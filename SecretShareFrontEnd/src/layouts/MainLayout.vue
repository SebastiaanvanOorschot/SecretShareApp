<template>
    <head>
    </head>
        <body>     
            <div class="column">   
                <div class="column helpTextContainer" v-if="$route.path == '/'">
                    <p>
                        This is a website meant for the safe transportation of secret information like passwords. Any string entered will be encrypted and stored. It can then only be retrieved through the correct passphrase, so..
                    </p>
                </div>
                <div class="column">                    
                    <RouterLink to="/secretShare" class="row submitButton" style="--clr:var(--accent)"><span>Tell me your secret..</span><i></i></RouterLink>
                </div>
                <div>
                    <RouterView />
                </div>
            </div>  
        </body>
</template>

<script setup lang="ts">
import { RouterLink } from 'vue-router';

</script>

<style>
</style>