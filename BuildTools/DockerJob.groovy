
node {
    docker.withRegistry('https://registry.hub.docker.com', 'docker_hub') {
        stage('Backend (release)') {
            def dockerfile = 'Dockerfile.Backend'
            def img = docker.build("chibicitiberiu/regard-backend:${env.BUILD_ID}", "-f ${dockerfile} --build-arg CONFIG=Release .")
            img.push('latest')
        }
        
        stage('Frontend (release)') {
            def dockerfile = 'Dockerfile.Frontend'
            def img = docker.build("chibicitiberiu/regard-frontend:${env.BUILD_ID}", "-f ${dockerfile} --build-arg CONFIG=Release .")
            img.push('latest')
        }

        stage('Backend (debug)') {
            def dockerfile = 'Dockerfile.Backend'
            def img = docker.build("chibicitiberiu/regard-backend:${env.BUILD_ID}-debug", "-f ${dockerfile} --build-arg CONFIG=Debug .")
            img.push('latest-debug')
        }
        
        stage('Frontend (debug)') {
            def dockerfile = 'Dockerfile.Frontend'
            def img = docker.build("chibicitiberiu/regard-frontend:${env.BUILD_ID}-debug", "-f ${dockerfile} --build-arg CONFIG=Debug .")
            img.push('latest-debug')
        }
    }
}
