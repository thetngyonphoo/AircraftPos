import * as cdk from 'aws-cdk-lib/core';
import { Construct } from 'constructs';
import * as s3 from 'aws-cdk-lib/aws-s3';
import * as lambda from 'aws-cdk-lib/aws-lambda';
import * as s3n from 'aws-cdk-lib/aws-s3-notifications';

export class InfraStack extends cdk.Stack {
  constructor(scope: Construct, id: string, props?: cdk.StackProps) {
    super(scope, id, props);

    const posbucket = new s3.Bucket(this, 'PosBucket', {

    });

    // Parser Lambda
    const parserLambda = new lambda.Function(this, 'ParserLambda', {
      runtime: lambda.Runtime.DOTNET_8,
      handler:
        'AircraftPos.ParserLambda::AircraftPos.ParserLambda.Function::FunctionHandler',
      code: lambda.Code.fromAsset(
        '../src/AircraftPos.ParserLambda/bin/Release/net8.0/publish'
      ),
    });

    // Allow Lambda to read/write the bucket
    posbucket.grantReadWrite(parserLambda);

    // Trigger Lambda only when an object is created under pos/
    posbucket.addEventNotification(
      s3.EventType.OBJECT_CREATED,
      new s3n.LambdaDestination(parserLambda),
      {
        prefix: 'pos/',
      }
    );

  }
}
