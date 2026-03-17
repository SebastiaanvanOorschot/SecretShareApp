const routes = [
    { 
        path: '/',
        component: () => import('../Layouts/MainLayout.vue'),
        children: [
            { 
                path: '/secretShare',
                component: () => import('../views/secretShare.vue')
            },
            { 
                path: '/secretPickup/:hash?',
                component: () => import('../views/secretPickup.vue')
            }
        ]
    }    
]

export default routes