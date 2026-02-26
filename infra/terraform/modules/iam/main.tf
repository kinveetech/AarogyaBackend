data "aws_caller_identity" "current" {}
data "aws_region" "current" {}

resource "aws_iam_user" "api" {
  name = "${var.project}-${var.environment}-api"
}

resource "aws_iam_access_key" "api" {
  user = aws_iam_user.api.name
}

resource "aws_iam_user_policy" "api" {
  name   = "${var.project}-${var.environment}-api-policy"
  user   = aws_iam_user.api.name
  policy = data.aws_iam_policy_document.api.json
}

data "aws_iam_policy_document" "api" {
  # S3 — report file storage
  statement {
    sid    = "S3ReportsBucket"
    effect = "Allow"
    actions = [
      "s3:GetObject",
      "s3:PutObject",
      "s3:DeleteObject",
      "s3:ListBucket",
      "s3:GetBucketAcl",
      "s3:GetBucketLocation",
      "s3:GetBucketNotification",
      "s3:PutBucketNotification",
    ]
    resources = [var.s3_bucket_arn, "${var.s3_bucket_arn}/*"]
  }

  # S3 — quarantine bucket (virus scanning)
  statement {
    sid    = "S3QuarantineBucket"
    effect = "Allow"
    actions = [
      "s3:GetBucketAcl",
      "s3:CreateBucket",
      "s3:PutObject",
    ]
    resources = [var.s3_quarantine_bucket_arn, "${var.s3_quarantine_bucket_arn}/*"]
  }

  # SQS — report upload event processing
  statement {
    sid    = "SQSQueue"
    effect = "Allow"
    actions = [
      "sqs:SendMessage",
      "sqs:ReceiveMessage",
      "sqs:DeleteMessage",
      "sqs:GetQueueUrl",
      "sqs:GetQueueAttributes",
      "sqs:SetQueueAttributes",
      "sqs:CreateQueue",
      "sqs:ChangeMessageVisibility",
    ]
    resources = [var.sqs_queue_arn]
  }

  # KMS — data encryption / decryption
  statement {
    sid    = "KMSDataKey"
    effect = "Allow"
    actions = [
      "kms:Encrypt",
      "kms:Decrypt",
      "kms:GenerateDataKey",
      "kms:GenerateDataKeyWithoutPlaintext",
      "kms:DescribeKey",
    ]
    resources = [var.kms_key_arn]
  }

  # Cognito — user management
  statement {
    sid    = "CognitoUserPool"
    effect = "Allow"
    actions = [
      "cognito-idp:AdminCreateUser",
      "cognito-idp:AdminGetUser",
      "cognito-idp:AdminUpdateUserAttributes",
      "cognito-idp:AdminDisableUser",
      "cognito-idp:AdminEnableUser",
      "cognito-idp:AdminDeleteUser",
      "cognito-idp:AdminSetUserPassword",
      "cognito-idp:AdminInitiateAuth",
      "cognito-idp:AdminRespondToAuthChallenge",
      "cognito-idp:ListUsers",
      "cognito-idp:DescribeUserPool",
      "cognito-idp:DescribeUserPoolClient",
    ]
    resources = [var.cognito_pool_arn]
  }

  # SES — transactional email
  statement {
    sid    = "SESSendEmail"
    effect = "Allow"
    actions = [
      "ses:SendEmail",
      "ses:SendRawEmail",
      "ses:SendTemplatedEmail",
    ]
    resources = [
      "arn:aws:ses:${data.aws_region.current.name}:${data.aws_caller_identity.current.account_id}:identity/${var.ses_domain}",
    ]
  }

  # CloudFront — cache invalidation (conditional)
  dynamic "statement" {
    for_each = var.cloudfront_distribution_arn != "" ? [1] : []
    content {
      sid    = "CloudFrontInvalidation"
      effect = "Allow"
      actions = [
        "cloudfront:CreateInvalidation",
        "cloudfront:GetInvalidation",
      ]
      resources = [var.cloudfront_distribution_arn]
    }
  }
}
