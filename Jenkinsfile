pipeline {
    agent none
    stages {
        stage('Compile') {
            agent {
                docker { image 'dotnet/sdk' }
            }
            steps {
                sh 'dotnet build Regard.sln'
            }
        }
    }
}