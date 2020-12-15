pipeline {
    agent none
    stages {
        stage('Debug compile') {
            agent {
                docker { 
                    image 'mcr.microsoft.com/dotnet/sdk' 
                    args '-u root:root'
                }
            }
            steps {
                sh 'dotnet publish Regard.sln -c Debug -o Build/Debug'
                archiveArtifacts artifacts: 'Build/Debug/**/*.*', fingerprint: true, onlyIfSuccessful: true
            }
        }
        
        stage('Release compile') {
            agent {
                docker { 
                    image 'mcr.microsoft.com/dotnet/sdk' 
                    args '-u root:root'
                }
            }
            steps {
                sh 'dotnet publish Regard.sln -c Release -o Build/Release'
                archiveArtifacts artifacts: 'Build/Release/**/*.*', fingerprint: true, onlyIfSuccessful: true
            }
        }
    }
}