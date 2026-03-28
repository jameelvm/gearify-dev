# S3 Module Outputs

output "bucket_arns" {
  description = "Map of bucket names to ARNs"
  value       = { for k, v in aws_s3_bucket.buckets : k => v.arn }
}

output "bucket_names" {
  description = "Map of bucket keys to bucket names"
  value       = { for k, v in aws_s3_bucket.buckets : k => v.bucket }
}

output "bucket_ids" {
  description = "Map of bucket keys to bucket IDs"
  value       = { for k, v in aws_s3_bucket.buckets : k => v.id }
}

output "bucket_domain_names" {
  description = "Map of bucket keys to regional domain names"
  value       = { for k, v in aws_s3_bucket.buckets : k => v.bucket_regional_domain_name }
}

output "s3_access_policy_arn" {
  description = "ARN of the S3 access IAM policy"
  value       = aws_iam_policy.s3_access.arn
}

output "product_images_bucket_name" {
  description = "Name of the product images bucket"
  value       = aws_s3_bucket.buckets["product-images"].bucket
}

output "tenant_assets_bucket_name" {
  description = "Name of the tenant assets bucket"
  value       = aws_s3_bucket.buckets["tenant-assets"].bucket
}

output "order_documents_bucket_name" {
  description = "Name of the order documents bucket"
  value       = aws_s3_bucket.buckets["order-documents"].bucket
}
