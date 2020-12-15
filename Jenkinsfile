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
                archiveArtifacts artifacts: 'Backend/Debug/**/*.*', fingerprint: true, onlyIfSuccessful: true
            }
        }

        stage('Frontend debug') {
            steps {
                sh 'dotnet publish Frontend.sln -c Debug -o Frontend/Debug'
                archiveArtifacts artifacts: 'Frontend/Debug/**/*.*', fingerprint: true, onlyIfSuccessful: true
            }
        }
        
        stage('Backend release') {
            steps {
                sh 'dotnet publish Backend.sln -c Release -o Backend/Release'
                archiveArtifacts artifacts: 'Backend/Release/**/*.*', fingerprint: true, onlyIfSuccessful: true
            }
        }
        
        stage('Frontend release') {
            steps {
                sh 'dotnet publish Frontend.sln -c Release -o Frontend/Release'
                archiveArtifacts artifacts: 'Frontend/Release/**/*.*', fingerprint: true, onlyIfSuccessful: true
            }
        }
    }
}