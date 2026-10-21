output "state_bucket_name" {
  description = "Bucket do state remoto: use em `bucket` no infra/terraform/backend.hcl."
  value       = aws_s3_bucket.tfstate.bucket
}
