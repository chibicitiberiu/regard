properties([pipelineTriggers([githubPush()])])

pipeline {
    agent {
        docker { 
            image 'mcr.microsoft.com/dotnet/sdk:5.0' 
            args '-u root:root'
        }
    }
    stages {
        stage('Backend debug') {
            steps {
                sh 'dotnet restore Source/Backend.sln'
                sh 'dotnet publish Source/Backend.sln -c Debug -o Build/Debug/Backend'
                archiveArtifacts artifacts: 'Build/Debug/Backend/**/*.*', fingerprint: true, onlyIfSuccessful: true
            }
        }

        stage('Frontend debug') {
            steps {
                sh 'dotnet restore Source/Frontend.sln'
                sh 'dotnet publish Source/Frontend.sln -c Debug -o Build/Debug/Frontend'
                archiveArtifacts artifacts: 'Build/Debug/Frontend/**/*.*', fingerprint: true, onlyIfSuccessful: true
            }
        }
        
        stage('Backend release') {
            steps {
                sh 'dotnet restore Source/Backend.sln'
                sh 'dotnet publish Source/Backend.sln -c Release -o Build/Release/Backend'
                archiveArtifacts artifacts: 'Build/Release/Backend/**/*.*', fingerprint: true, onlyIfSuccessful: true
            }
        }
        
        stage('Frontend release') {
            steps {
                sh 'dotnet restore Source/Frontend.sln'
                sh 'dotnet publish Source/Frontend.sln -c Release -o Build/Release/Frontend'
                archiveArtifacts artifacts: 'Build/Release/Frontend/**/*.*', fingerprint: true, onlyIfSuccessful: true
            }
        }
    }
    post {
        always {
            sh 'chown -R 1000:1000 *'
        }
    }
}