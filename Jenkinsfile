pipeline {
    agent none
    stages {
        stage('Debug build') {
            agent {
                docker { 
                    image 'mcr.microsoft.com/dotnet/sdk' 
                    args '-u root:root'
                }
            }
            steps {
                sh 'dotnet build Regard.sln -c Debug -o ./Build/Debug'
                sh 'dotnet build Regard.sln -c Release -o ./Build/Release'
            }
        }
    }
    post {
        always {
            archiveArtifacts artifacts: 'Build/**/*.*', fingerprint: true, onlyIfSuccessful: true
        }
    }
}