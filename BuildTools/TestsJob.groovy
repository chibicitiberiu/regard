pipeline {
    agent {
        docker { 
            image 'mcr.microsoft.com/dotnet/sdk:5.0' 
            args '-u root:root'
        }
    }
    stages {
        stage('Copy artifacts') {
            steps {
                copyArtifacts fingerprintArtifacts: true,
                    projectName: 'Regard/master',  
                    selector: lastSuccessful()
            }
        }

        stage('Regard.Backend.Tests') {
            steps {
                sh 'dotnet test Backend/Debug/Regard.Backend.Tests.dll --results-directory TestResults --logger trx --verbosity normal'
            }
        }

        stage('YoutubeDLWrapper.Tests') {
            steps {
                sh 'dotnet test Backend/Debug/YoutubeDLWrapper.Tests.dll --results-directory TestResults --logger trx --verbosity normal'
            }
        }
    }
    post {
        always {
            archiveArtifacts artifacts: 'TestResults/**/*.*', fingerprint: true, onlyIfSuccessful: false
            sh 'chown -R 1000:1000 *'
        }
    }
}