provider "aws" {
  region = var.region
}

resource "aws_dynamodb_table" "products" {
  name           = "gearify-products"
  billing_mode   = "PAY_PER_REQUEST"
  hash_key       = "Id"
  attribute {
    name = "Id"
    type = "S"
  }
}
