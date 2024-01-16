#!/usr/bin/env node
import 'source-map-support/register';
import * as cdk from 'aws-cdk-lib';
import { MessageHandlerStack } from './stack/message-handler-stack';

const app = new cdk.App();
new MessageHandlerStack(app, 'MessageHandlerStack');