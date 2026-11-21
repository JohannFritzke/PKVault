# tenancy_ocid, region and compartment_ocid are filled in automatically by Resource Manager.
variable "tenancy_ocid" {
  type = string
}

variable "region" {
  type = string
}

variable "compartment_ocid" {
  type = string
}

variable "ssh_public_key" {
  type        = string
  description = "Contents of your SSH public key (the .pub file)."
}

variable "instance_name" {
  type    = string
  default = "pokehome"
}

variable "ad_number" {
  type        = number
  default     = 1
  description = "Availability domain (1, 2 or 3). If creation fails with 'Out of capacity', try another number."
}
