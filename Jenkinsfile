pipeline {
    agent {
        docker { 
            image 'mcr.microsoft.com/dotnet/sdk' 
            args '-u root:root'
        }
    }
    stages {
        stage('Backend debug') {
            steps {
                sh 'dotnet publish Backend.sln -c Debug -o Backend/Debug'
                sh 'chown -R 1000:1000 *'
                archiveArtifacts artifacts: 'Backend/Debug/**/*.*', fingerprint: true, onlyIfSuccessful: true
            }
        }

        stage('Frontend debug') {
            steps {
                sh 'dotnet publish Frontend.sln -c Debug -o Frontend/Debug'
                sh 'chown -R 1000:1000 *'
                archiveArtifacts artifacts: 'Frontend/Debug/**/*.*', fingerprint: true, onlyIfSuccessful: true
            }
        }
        
        stage('Backend release') {
            steps {
                sh 'dotnet publish Backend.sln -c Release -o Backend/Release'
                sh 'chown -R 1000:1000 *'
                archiveArtifacts artifacts: 'Backend/Release/**/*.*', fingerprint: true, onlyIfSuccessful: true
            }
        }
        
        stage('Frontend release') {
            steps {
                sh 'dotnet publish Frontend.sln -c Release -o Frontend/Release'
                sh 'chown -R 1000:1000 *'
                archiveArtifacts artifacts: 'Frontend/Release/**/*.*', fingerprint: true, onlyIfSuccessful: true
            }
        }
    }
}