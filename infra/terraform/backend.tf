# One-time bootstrap (run manually before first `terraform init`):
#
#   aws s3api create-bucket \
#     --bucket aarogya-terraform-state \
#     --region ap-south-1 \
#     --create-bucket-configuration LocationConstraint=ap-south-1
#
#   aws s3api put-bucket-versioning \
#     --bucket aarogya-terraform-state \
#     --versioning-configuration Status=Enabled
#
#   aws dynamodb create-table \
#     --table-name aarogya-terraform-locks \
#     --attribute-definitions AttributeName=LockID,AttributeType=S \
#     --key-schema AttributeName=LockID,KeyType=HASH \
#     --billing-mode PAY_PER_REQUEST \
#     --region ap-south-1

terraform {
  backend "s3" {
    bucket         = "aarogya-terraform-state"
    key            = "dev/terraform.tfstate"
    region         = "ap-south-1"
    dynamodb_table = "aarogya-terraform-locks"
    encrypt        = true
  }
}
