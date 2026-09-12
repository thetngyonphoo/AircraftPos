using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AircraftPos.Api.Controllers
{
    [Route("status")]
    [ApiController]
    public class AircraftStatusController : ControllerBase
    {
        private readonly IAmazonDynamoDB _dynamoDb;
        private readonly IAmazonS3 _s3Client;

        private readonly string _parsedRecordsTable;
        private readonly string _calculationResultsTable;
        private readonly string _posBucketName;

        public AircraftStatusController()
        {
            _dynamoDb = new AmazonDynamoDBClient();
            _s3Client = new AmazonS3Client();

            _parsedRecordsTable =
                Environment.GetEnvironmentVariable("PARSED_RECORDS_TABLE")
                ?? throw new InvalidOperationException(
                    "PARSED_RECORDS_TABLE environment variable is not configured.");

            _calculationResultsTable =
                Environment.GetEnvironmentVariable("CALCULATION_RESULTS_TABLE")
                ?? throw new InvalidOperationException(
                    "CALCULATION_RESULTS_TABLE environment variable is not configured.");

            _posBucketName =
                Environment.GetEnvironmentVariable("POS_BUCKET_NAME")
                ?? throw new InvalidOperationException(
                    "POS_BUCKET_NAME environment variable is not configured.");
        }

        [HttpGet("{flightId}")]
        public async Task<IActionResult> GetStatus(string flightId)
        {
            var parsedRecord = await GetLatestRecordAsync(_parsedRecordsTable, flightId);

            var calculationRecord = await GetLatestRecordAsync(_calculationResultsTable, flightId);

            if (parsedRecord is null && calculationRecord is null)
            {
                return NotFound();
            }

            // If a calculation result exists, return it.
            if (calculationRecord is not null)
            {
                var attachment =
                    calculationRecord["attachment"].S;

                var resultJson = await GetResultFromS3Async(attachment);

                return Content(
                    resultJson,
                    "application/json");
            }

            // Calculator has not run yet.
            // Return the latest parsed state.
            return Ok(parsedRecord);
        }

        private async Task<Dictionary<string, AttributeValue>?> GetLatestRecordAsync(string tableName, string flightId)
        {
            var request = new QueryRequest
            {
                TableName = tableName,

                KeyConditionExpression = "flightId = :flightId",

                ExpressionAttributeValues =
                    new Dictionary<string, AttributeValue>
                    {
                        [":flightId"] = new AttributeValue
                        {
                            S = flightId
                        }
                    },

                ScanIndexForward = false,
                Limit = 1
            };

            var response =
                await _dynamoDb.QueryAsync(request);

            return response.Items.Count == 0
                ? null
                : response.Items[0];
        }

        private async Task<string> GetResultFromS3Async(string attachment)
        {
            var request = new GetObjectRequest
            {
                BucketName = _posBucketName,
                Key = attachment
            };

            using var response = await _s3Client.GetObjectAsync(request);

            using var reader =
                new StreamReader(response.ResponseStream);

            return await reader.ReadToEndAsync();
        }
    }
}
