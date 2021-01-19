pipeline {
    agent {
        docker { 
            image 'mcr.microsoft.com/dotnet/sdk' 
            args '-u root:root'
        }
    }
    stages {
        stage('Copy artifacts') {
            steps {
                copyArtifacts fingerprintArtifacts: true,
                    projectName: 'Regard/master',  
                    selector: upstream()
            }
        }

        stage('Regard.Backend.Tests') {
            steps {
                sh 'dotnet test Backend/Debug/Regard.Backend.Tests.dll --results-directory TestResults --verbosity normal'
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