# Aircraft POS Processing Service

A serverless AWS application that receives aircraft POS messages, parses the data, calculates fuel information, and stores flight information using AWS managed services.

## Project Structure

```text
AircraftPos/
├── infra/
├── src/
│   ├── AircraftPos.Api/
│   ├── AircraftPos.Application/
│   ├── AircraftPos.Domain/
│   ├── AircraftPos.Infrastructure/
│   ├── AircraftPos.ParserLambda/
│   └── AircraftPos.CalculatorLambda/
├── tests/
│   └── AircraftPos.Tests/
└── README.md
```

## Architecture

```text
      Client
   │
   │ POST POS report
   ▼
API Gateway
   │
   ▼
API Lambda
   │
   ├──────────────► S3 (raw POS message)
   │
   ▼
Parser / processing
   │
   ▼
SQS
   │
   ▼
Calculator Lambda
   │
   ├──────────────► Parsed DynamoDB
   ├──────────────► Calculation Results DynamoDB
   └──────────────► S3
                        

Client
   │
   │ GET /status/{flightId}
   ▼
API Gateway
   │
   ▼
API Lambda
   │
   └──────────────► DynamoDB / S3
```

## Technologies

- .NET 8 / C#
- AWS Lambda
- Amazon API Gateway
- Amazon S3
- Amazon DynamoDB
- Amazon SQS
- AWS CDK / TypeScript
- xUnit

## Prerequisites

- .NET 8 SDK
- Node.js and npm
- AWS CLI
- AWS CDK
- Configured AWS credentials

## Local Development

### Build

```bash
dotnet restore
dotnet build
```

### Run Unit Tests

```bash
dotnet test
```

### Run API Locally

```bash
dotnet run --project src/AircraftPos.Api
```

## AWS Deployment

### Configure AWS

```bash
aws configure
aws sts get-caller-identity
```

### Publish Lambda Projects

Before deploying, publish the three Lambda projects:

```bash
dotnet publish src/AircraftPos.Api/AircraftPos.Api.csproj -c Release
dotnet publish src/AircraftPos.ParserLambda/AircraftPos.ParserLambda.csproj -c Release
dotnet publish src/AircraftPos.CalculatorLambda/AircraftPos.CalculatorLambda.csproj -c Release
```

### Install CDK Dependencies

```bash
cd infra
npm install
```

### Bootstrap CDK

```bash
cdk bootstrap
```

### Synthesize Infrastructure

```bash
cdk synth
```

### Deploy

```bash
cdk deploy
```

The deployed API URL will be displayed in the CDK outputs.

## Testing

### POS Message Example

```text
POS/UL604.FR RGN/TO BKK/121021/N1642.3E09612.5/550/12500/2800
```

The POS message is parsed and processed by the application.

### Check Flight Status

After deployment, the flight status can be retrieved using:

```text
GET <API_URL>/status/{flightId}
```

Example:

```text
GET <API_URL>/status/UL60420260912RGNBKK
```

Unit tests cover the POS report parser, including valid and invalid input scenarios.

## Key Decisions

- AWS Lambda is used for serverless processing.
- Amazon SQS provides asynchronous processing between the Parser and Calculator.
- Amazon DynamoDB stores parsed flight data and calculation results.
- Amazon S3 stores POS-related data and calculation result files.
- AWS CDK manages the AWS infrastructure as code.
- The application is separated into Domain, Application, API, and Lambda projects for maintainability.

## Failure Handling

- Invalid POS messages are rejected during parsing.
- SQS provides retry handling for asynchronous processing.
- A dead-letter queue is used for messages that repeatedly fail processing.
- POS data stored in S3 can be used for troubleshooting and traceability.

## Assumptions

- POS messages follow the expected format.
- Coordinates use the expected aviation format.
- Fuel values are provided in kilograms.
- Ground speed is provided in knots.
- POS timestamps are treated as UTC.

## Notes

This project was developed as part of a technical assessment to demonstrate .NET development, AWS serverless architecture, infrastructure as code, asynchronous processing, and unit testing.