# CloudFront Report CDN

CloudFormation template to provision a CloudFront distribution for report-file serving with:

- S3 origin protected via Origin Access Identity (OAI)
- HTTPS-only viewer access
- Signed URL enforcement (`TrustedSigners: self`)
- `Cache-Control: private, no-store, max-age=0` response header

## Deploy

```bash
aws cloudformation deploy \
  --stack-name aarogya-report-cdn \
  --template-file infra/aws/cloudfront-report-cdn/cloudfront-report-cdn.yaml \
  --parameter-overrides ReportsBucketName=<reports-bucket-name> \
  --capabilities CAPABILITY_NAMED_IAM
```

## Wire API Configuration

After deployment, set:

- `Aws:S3:CloudFront:Enabled=true`
- `Aws:S3:CloudFront:DistributionId=<DistributionId>`
- `Aws:S3:CloudFront:DistributionDomain=<DistributionDomainName>`
- `Aws:S3:CloudFront:KeyPairId=<CloudFront key pair id>`
- `Aws:S3:CloudFront:PrivateKeyPem=<base64 PEM>`
- `Aws:S3:CloudFront:EnableInvalidationOnDelete=true`
