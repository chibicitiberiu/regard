
node {
    stage('Backend') {

        def dockerfile = 'Dockerfile.Backend'
        def customImage = docker.build("regard-backend:${env.BUILD_ID}", "-f ${dockerfile} .")

    }
    
    stage('Frontend') {

        def dockerfile = 'Dockerfile.Frontend'
        def customImage = docker.build("regard-frontend:${env.BUILD_ID}", "-f ${dockerfile} .")

    }
}
