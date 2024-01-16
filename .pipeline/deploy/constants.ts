export const References = {
  SQS: 'event-queue',
  LambdaDeployBucket: {
    ProfileApi: 'lambda-deploy-bucket-profile-api',
    MessageHandler: 'lambda-deploy-bucket-message-handler',
  },
  Tables: {
    Profiles: 'profiles-table',
    Messages: 'messages-table',
  },
};