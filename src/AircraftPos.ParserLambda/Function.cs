using AircraftPos.Application.Interfaces;
using AircraftPos.Application.Services;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using System.Text.Json;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace AircraftPos.ParserLambda
{
    public class Function
    {
        private readonly IAmazonS3 _s3Client;
        private readonly IAmazonDynamoDB _dynamoDBClient;
        private readonly IAmazonSQS _sqsClient;
        private readonly IPosReportParser _posReportParser;
        public Function()
        {
            _s3Client = new AmazonS3Client();
            _dynamoDBClient = new AmazonDynamoDBClient();
            _sqsClient = new AmazonSQSClient();
            _posReportParser = new PosReportParser();
        }

        /// <summary>
        /// A simple function that takes a string and does a ToUpper
        /// </summary>
        /// <param name="input">The event for the Lambda function handler to process.</param>
        /// <param name="context">The ILambdaContext that provides methods for logging and describing the Lambda environment.</param>
        /// <returns></returns>
        public async Task FunctionHandler(S3Event s3Event, ILambdaContext context)
        {
            foreach (var record in s3Event.Records)
            {
                var bucketName = record.S3.Bucket.Name;
                var objectKey = record.S3.Object.Key;

                context.Logger.LogInformation($"S3 object created: {bucketName}/{objectKey}");

                var response = await _s3Client.GetObjectAsync(
                    new GetObjectRequest
                    {
                        BucketName = bucketName,
                        Key = objectKey
                    });

                using var reader = new StreamReader(response.ResponseStream);
                var rawMessage = await reader.ReadToEndAsync();
                context.Logger.LogInformation($"Raw POS message: {rawMessage}");

                var posReport = _posReportParser.Parse(rawMessage, DateTime.UtcNow);
                context.Logger.LogInformation($"Parsed flight: {posReport.FlightNumber}");

                // Convert PosReport to JSON
                var json = JsonSerializer.Serialize(posReport);

                // Create attachment S3 key
                var attachmentKey =
                    $"attachment/{posReport.FlightId}-{posReport.Timestamp:yyyy-MM-ddTHH:mm:ssZ}.json";

                // Save JSON to S3
                await _s3Client.PutObjectAsync(
                    new PutObjectRequest
                    {
                        BucketName = bucketName,
                        Key = attachmentKey,
                        ContentBody = json,
                        ContentType = "application/json"
                    });
                context.Logger.LogInformation($"Attachment saved to S3: {bucketName}/{attachmentKey}");

                //Save parsed record to DynamoDB
                var tableName = Environment.GetEnvironmentVariable("PARSED_TABLE_NAME");

                if (string.IsNullOrWhiteSpace(tableName))
                {
                    throw new InvalidOperationException(
                        "PARSED_TABLE_NAME environment variable is not configured.");
                }

                var timestamp = posReport.Timestamp.ToString("yyyy-MM-ddTHH:mm:ssZ");

                var putItemRequest = new PutItemRequest
                {
                    TableName = tableName,
                    Item = new Dictionary<string, AttributeValue>
                    {
                        ["flightId"] = new AttributeValue
                        {
                            S = posReport.FlightId
                        },
                        ["timestamp"] = new AttributeValue
                        {
                            S = timestamp
                        },
                        ["attachment"] = new AttributeValue
                        {
                            S = attachmentKey
                        }
                    }
                };

                await _dynamoDBClient.PutItemAsync(putItemRequest);

                context.Logger.LogInformation($"Parsed record saved to DynamoDB: {posReport.FlightId}");

                // Send message to SQS
                var queueUrl = Environment.GetEnvironmentVariable("SQS_QUEUE_URL");

                if (string.IsNullOrWhiteSpace(queueUrl))
                {
                    throw new InvalidOperationException(
                        "SQS_QUEUE_URL environment variable is not configured.");
                }

                var message = new
                {
                    flightId = posReport.FlightId,
                    timestamp = timestamp
                };

                var messageBody = JsonSerializer.Serialize(message);

                await _sqsClient.SendMessageAsync(
                    new SendMessageRequest
                    {
                        QueueUrl = queueUrl,
                        MessageBody = messageBody
                    });

                context.Logger.LogInformation($"Message sent to SQS: {messageBody}");

            }
        }
    }
}
