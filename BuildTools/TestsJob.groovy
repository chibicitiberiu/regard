pipeline {
    agent {
        docker { 
            image 'mcr.microsoft.com/dotnet/sdk:5.0' 
            args '-u root:root'
        }
    }
    stages {
        stage('Install dependencies') {
            steps {
                sh 'apt-get clean && apt-get update && apt-get install -y python3'
            }
        }
        stage('Copy artifacts') {
            steps {
                copyArtifacts fingerprintArtifacts: true,
                    projectName: 'Regard/master',  
                    selector: lastSuccessful()
            }
        }

        stage('Regard.Backend.Tests') {
            steps {
                sh 'dotnet test Build/Debug/Backend/Regard.Backend.Tests.dll --results-directory TestResults --logger trx --verbosity normal'
            }
        }

        stage('YoutubeDLWrapper.Tests') {
            steps {
                sh 'dotnet test Build/Debug/Backend/YoutubeDLWrapper.Tests.dll --results-directory TestResults --logger trx --verbosity normal'
            }
        }
    }
    post {
        always {
            sh 'chown -R 1000:1000 *'
            archiveArtifacts artifacts: 'TestResults/**/*.*', fingerprint: true, onlyIfSuccessful: false
            mstest testResultsFile: 'TestResults/**/*.trx', keepLongStdio: true
        }
    }
}