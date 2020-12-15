pipeline {
    agent none
    stages {
        stage('Compile') {
            agent {
                docker { image 'mcr.microsoft.com/dotnet/sdk' }
            }
            steps {
                sh 'dotnet build Regard.sln'
            }
        }
    }
}