import * as cdk from 'aws-cdk-lib/core';
import { Construct } from 'constructs';
import * as s3 from 'aws-cdk-lib/aws-s3';
import * as lambda from 'aws-cdk-lib/aws-lambda';
import * as s3n from 'aws-cdk-lib/aws-s3-notifications';
import * as dynamodb from 'aws-cdk-lib/aws-dynamodb';
import * as sqs from 'aws-cdk-lib/aws-sqs';
import * as lambdaEventSources from 'aws-cdk-lib/aws-lambda-event-sources';
import * as apigateway from 'aws-cdk-lib/aws-apigateway';

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
      timeout: cdk.Duration.seconds(30),
    });

    // Parsed DynamoDB table
    const parsedTable = new dynamodb.Table(this, 'ParsedTable', {
      partitionKey: {
        name: 'flightId',
        type: dynamodb.AttributeType.STRING,
      },
      sortKey: {
        name: 'timestamp',
        type: dynamodb.AttributeType.STRING,
      },
      billingMode: dynamodb.BillingMode.PAY_PER_REQUEST,
    });

    // Allow Parser Lambda to write to DynamoDB
    parsedTable.grantWriteData(parserLambda);

    // Give Lambda the table name
    parserLambda.addEnvironment(
      'PARSED_TABLE_NAME',
      parsedTable.tableName
    );

    // Dead-Letter Queue
    const deadLetterQueue = new sqs.Queue(this, 'PosDeadLetterQueue', {
      retentionPeriod: cdk.Duration.days(14),
    });

    // Main SQS Queue
    const posQueue = new sqs.Queue(this, 'PosQueue', {
      visibilityTimeout: cdk.Duration.seconds(60),

      deadLetterQueue: {
        queue: deadLetterQueue,
        maxReceiveCount: 3,
      },
    });

    // Allow Parser Lambda to send messages to SQS
    posQueue.grantSendMessages(parserLambda);

    // Give Lambda the SQS Queue URL
    parserLambda.addEnvironment(
      'SQS_QUEUE_URL',
      posQueue.queueUrl
    );

    // Allow Lambda to read/write the bucket
    posbucket.grantReadWrite(parserLambda);

    // Calculator Lambda
    const calculatorLambda = new lambda.Function(this, 'CalculatorLambda', {
      runtime: lambda.Runtime.DOTNET_8,
      handler:
        'AircraftPos.CalculatorLambda::AircraftPos.CalculatorLambda.Function::FunctionHandler',
      code: lambda.Code.fromAsset(
        '../src/AircraftPos.CalculatorLambda/bin/Release/net8.0/publish'
      ),
      timeout: cdk.Duration.seconds(30),
    });

    // Give Calculator Lambda the Parsed DynamoDB table name
    calculatorLambda.addEnvironment(
      'PARSED_RECORDS_TABLE',
      parsedTable.tableName
    );

    // Allow Calculator Lambda to read Parsed DynamoDB
    parsedTable.grantReadData(calculatorLambda);

    // Allow Calculator Lambda to consume messages from SQS
    posQueue.grantConsumeMessages(calculatorLambda);

    // Trigger Calculator Lambda from SQS
    calculatorLambda.addEventSource(
      new lambdaEventSources.SqsEventSource(posQueue, {
        batchSize: 10,
      })
    );

    // S3 bucket
    calculatorLambda.addEnvironment(
      'POS_BUCKET_NAME',
      posbucket.bucketName
    );

    posbucket.grantReadWrite(calculatorLambda);

    // Calculation Results DynamoDB
    const calculationResultsTable = new dynamodb.Table(
      this,
      'CalculationResultsTable',
      {
        partitionKey: {
          name: 'flightId',
          type: dynamodb.AttributeType.STRING,
        },
        sortKey: {
          name: 'timestamp',
          type: dynamodb.AttributeType.STRING,
        },
        billingMode: dynamodb.BillingMode.PAY_PER_REQUEST,
      }
    );

    calculationResultsTable.grantWriteData(calculatorLambda);

    calculatorLambda.addEnvironment(
      'CALCULATION_RESULTS_TABLE',
      calculationResultsTable.tableName
    );

    // API Lambda
    const apiLambda = new lambda.Function(this, 'ApiLambda', {
      runtime: lambda.Runtime.DOTNET_8,

      handler:
        'AircraftPos.Api::AircraftPos.Api.LambdaEntryPoint::FunctionHandlerAsync',

      code: lambda.Code.fromAsset(
        '../src/AircraftPos.Api/bin/Release/net8.0/publish'
      ),

      timeout: cdk.Duration.seconds(30),
    });

    // Give API Lambda the names of the existing DynamoDB tables
    apiLambda.addEnvironment(
      'PARSED_RECORDS_TABLE',
      parsedTable.tableName
    );

    apiLambda.addEnvironment(
      'CALCULATION_RESULTS_TABLE',
      calculationResultsTable.tableName
    );
    
    apiLambda.addEnvironment(
      'POS_BUCKET_NAME',
      posbucket.bucketName
    );

    // Allow API Lambda to read the existing tables
    parsedTable.grantReadData(apiLambda);

    calculationResultsTable.grantReadData(apiLambda);

    posbucket.grantRead(apiLambda);

    // API Gateway
    const api = new apigateway.RestApi(this, 'AircraftPosApi', {
      restApiName: 'Aircraft POS API',
    });

    const statusResource = api.root.addResource('status');

    statusResource
      .addResource('{flightId}')
      .addMethod(
        'GET',
        new apigateway.LambdaIntegration(apiLambda)
      );

    // Trigger Lambda only when an object is created under pos/
    posbucket.addEventNotification(
      s3.EventType.OBJECT_CREATED,
      new s3n.LambdaDestination(parserLambda),
      {
        prefix: 'pos/',
      }
    );

    new cdk.CfnOutput(this, 'ApiUrl', {
      value: api.url,
    });

  }
}
