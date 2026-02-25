locals {
  name_prefix = "${var.project}-${var.environment}"
}

# --- KMS ---

module "kms" {
  source      = "./modules/kms"
  environment = var.environment
  project     = var.project
}

# --- S3 ---

module "s3" {
  source      = "./modules/s3"
  bucket_name = local.name_prefix
  kms_key_arn = module.kms.key_arn
}

# S3 bucket policy lives here (not in the S3 module) to avoid a circular
# dependency with CloudFront: CloudFront needs the S3 domain name, and the
# bucket policy needs the CloudFront OAI ARN.
resource "aws_s3_bucket_policy" "reports" {
  bucket = module.s3.bucket_id
  policy = data.aws_iam_policy_document.s3_bucket_policy.json

  depends_on = [module.s3]
}

data "aws_iam_policy_document" "s3_bucket_policy" {
  statement {
    sid    = "DenyInsecureTransport"
    effect = "Deny"

    principals {
      type        = "*"
      identifiers = ["*"]
    }

    actions   = ["s3:*"]
    resources = [module.s3.bucket_arn, "${module.s3.bucket_arn}/*"]

    condition {
      test     = "Bool"
      variable = "aws:SecureTransport"
      values   = ["false"]
    }
  }

  dynamic "statement" {
    for_each = var.cloudfront_enabled ? [1] : []
    content {
      sid    = "AllowCloudFrontRead"
      effect = "Allow"

      principals {
        type        = "AWS"
        identifiers = [module.cloudfront[0].oai_iam_arn]
      }

      actions   = ["s3:GetObject"]
      resources = ["${module.s3.bucket_arn}/reports/*"]
    }
  }
}

# --- SQS ---

module "sqs" {
  source        = "./modules/sqs"
  queue_name    = "${local.name_prefix}-queue"
  s3_bucket_arn = module.s3.bucket_arn
  s3_bucket_id  = module.s3.bucket_id
}

# --- Lambda (Cognito pre-signup) ---

module "lambda_pre_signup" {
  source      = "./modules/lambda-pre-signup"
  environment = var.environment
  project     = var.project
}

# --- Cognito ---

module "cognito" {
  source                = "./modules/cognito"
  environment           = var.environment
  project               = var.project
  pool_name             = "${local.name_prefix}-users"
  pre_signup_lambda_arn = module.lambda_pre_signup.function_arn
  google_client_id      = var.google_client_id
  google_client_secret  = var.google_client_secret
  callback_urls         = var.cognito_callback_urls
}

resource "aws_lambda_permission" "cognito_pre_signup" {
  statement_id  = "AllowCognitoInvoke"
  action        = "lambda:InvokeFunction"
  function_name = module.lambda_pre_signup.function_name
  principal     = "cognito-idp.amazonaws.com"
  source_arn    = module.cognito.user_pool_arn
}

# --- SES ---

module "ses" {
  source          = "./modules/ses"
  domain          = var.ses_domain
  route53_zone_id = var.route53_zone_id
}

# --- CloudFront (optional) ---

module "cloudfront" {
  count = var.cloudfront_enabled ? 1 : 0

  source                  = "./modules/cloudfront"
  environment             = var.environment
  project                 = var.project
  s3_bucket_domain_name   = module.s3.bucket_regional_domain_name
  logs_bucket_domain_name = var.cloudfront_logs_bucket_domain
}

# --- IAM (API server credentials) ---

module "iam" {
  source                      = "./modules/iam"
  environment                 = var.environment
  project                     = var.project
  s3_bucket_arn               = module.s3.bucket_arn
  sqs_queue_arn               = module.sqs.queue_arn
  kms_key_arn                 = module.kms.key_arn
  cognito_pool_arn            = module.cognito.user_pool_arn
  ses_domain                  = var.ses_domain
  cloudfront_distribution_arn = var.cloudfront_enabled ? module.cloudfront[0].distribution_arn : ""
}
