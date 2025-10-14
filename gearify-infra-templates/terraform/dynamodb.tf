resource "aws_dynamodb_table" "products" {
  name           = "gearify-products"
  billing_mode   = "PAY_PER_REQUEST"
  hash_key       = "Id"

  attribute {
    name = "Id"
    type = "S"
  }

  tags = {
    Name = "gearify-products"
  }
}

resource "aws_dynamodb_table" "carts" {
  name         = "gearify-carts"
  billing_mode = "PAY_PER_REQUEST"
  hash_key     = "UserId"

  attribute {
    name = "UserId"
    type = "S"
  }
}
