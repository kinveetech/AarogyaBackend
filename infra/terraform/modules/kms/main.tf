resource "aws_kms_key" "this" {
  description             = "${var.project} ${var.environment} data encryption key"
  deletion_window_in_days = 30
  enable_key_rotation     = true
}

resource "aws_kms_alias" "this" {
  name          = "alias/${var.project}-${var.environment}"
  target_key_id = aws_kms_key.this.key_id
}
