pipeline {
    node {
        checkout scm
        
        stage('Backend') {
            steps {
                def dockerfile = 'Dockerfile.Backend'
                def customImage = docker.build("regard-backend:${env.BUILD_ID}", "-f ${dockerfile} .")
            }
        }
        
        stage('Frontend') {
            steps {
                def dockerfile = 'Dockerfile.Frontend'
                def customImage = docker.build("regard-frontend:${env.BUILD_ID}", "-f ${dockerfile} .")
            }
        }
    }
}