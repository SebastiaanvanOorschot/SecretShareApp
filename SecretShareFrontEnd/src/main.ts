import { createApp } from 'vue'
import { FontAwesomeIcon } from '@fortawesome/vue-fontawesome'
import { library } from '@fortawesome/fontawesome-svg-core'
import { fas } from '@fortawesome/free-solid-svg-icons'
import { fab } from '@fortawesome/free-brands-svg-icons'
import { far } from '@fortawesome/free-regular-svg-icons'
import { createRouter, createWebHistory } from 'vue-router'
import App from './App.vue'
import routes from "./router/routes.js"

const router = createRouter({
    history: createWebHistory(),
    routes,
})  

/* add icons to the library */
library.add(fas, fab, far)

createApp(App)
.component('font-awesome-icon', FontAwesomeIcon)
.use(router)
.mount('#app')
