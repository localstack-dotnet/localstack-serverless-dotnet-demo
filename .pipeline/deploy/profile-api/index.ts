#!/usr/bin/env node
import 'source-map-support/register';
import * as cdk from 'aws-cdk-lib';
import { ProfileApiStack } from './stack/profile-api-stack';

const app = new cdk.App();
new ProfileApiStack(app, 'ProfileApiStack');