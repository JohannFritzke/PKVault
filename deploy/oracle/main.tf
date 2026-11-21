# PokéHome server on Oracle Cloud Always Free.
# Upload as a Resource Manager stack (Developer Services -> Resource Manager -> Stacks).
# Creates: VCN + internet gateway + public subnet (ports 22/80/443 open)
#          + one VM.Standard.E2.1.Micro (AMD, Always Free) running Ubuntu 24.04.

terraform {
  required_providers {
    oci = {
      source = "oracle/oci"
    }
  }
}

provider "oci" {
  region = var.region
}

locals {
  shape = "VM.Standard.E2.1.Micro"
}

data "oci_identity_availability_domains" "ads" {
  compartment_id = var.tenancy_ocid
}

# newest Canonical Ubuntu 24.04 (not "Minimal") image that runs on the micro shape
data "oci_core_images" "ubuntu" {
  compartment_id           = var.compartment_ocid
  operating_system         = "Canonical Ubuntu"
  operating_system_version = "24.04"
  shape                    = local.shape
  sort_by                  = "TIMECREATED"
  sort_order               = "DESC"
}

resource "oci_core_vcn" "pokehome" {
  compartment_id = var.compartment_ocid
  display_name   = "${var.instance_name}-vcn"
  cidr_blocks    = ["10.0.0.0/16"]
  dns_label      = "pokehome"
}

resource "oci_core_internet_gateway" "pokehome" {
  compartment_id = var.compartment_ocid
  vcn_id         = oci_core_vcn.pokehome.id
  display_name   = "${var.instance_name}-igw"
  enabled        = true
}

resource "oci_core_route_table" "pokehome" {
  compartment_id = var.compartment_ocid
  vcn_id         = oci_core_vcn.pokehome.id
  display_name   = "${var.instance_name}-routes"

  route_rules {
    destination       = "0.0.0.0/0"
    destination_type  = "CIDR_BLOCK"
    network_entity_id = oci_core_internet_gateway.pokehome.id
  }
}

resource "oci_core_security_list" "pokehome" {
  compartment_id = var.compartment_ocid
  vcn_id         = oci_core_vcn.pokehome.id
  display_name   = "${var.instance_name}-security"

  egress_security_rules {
    destination = "0.0.0.0/0"
    protocol    = "all"
  }

  # SSH
  ingress_security_rules {
    source   = "0.0.0.0/0"
    protocol = "6"
    tcp_options {
      min = 22
      max = 22
    }
  }

  # HTTP (Caddy uses it to get the HTTPS certificate, then redirects)
  ingress_security_rules {
    source   = "0.0.0.0/0"
    protocol = "6"
    tcp_options {
      min = 80
      max = 80
    }
  }

  # HTTPS
  ingress_security_rules {
    source   = "0.0.0.0/0"
    protocol = "6"
    tcp_options {
      min = 443
      max = 443
    }
  }

  # path MTU discovery (Oracle default)
  ingress_security_rules {
    source   = "0.0.0.0/0"
    protocol = "1"
    icmp_options {
      type = 3
      code = 4
    }
  }
}

resource "oci_core_subnet" "pokehome" {
  compartment_id    = var.compartment_ocid
  vcn_id            = oci_core_vcn.pokehome.id
  display_name      = "${var.instance_name}-public"
  cidr_block        = "10.0.0.0/24"
  dns_label         = "public"
  route_table_id    = oci_core_route_table.pokehome.id
  security_list_ids = [oci_core_security_list.pokehome.id]
}

resource "oci_core_instance" "pokehome" {
  compartment_id      = var.compartment_ocid
  availability_domain = data.oci_identity_availability_domains.ads.availability_domains[var.ad_number - 1].name
  display_name        = var.instance_name
  shape               = local.shape

  create_vnic_details {
    subnet_id        = oci_core_subnet.pokehome.id
    assign_public_ip = true
    hostname_label   = var.instance_name
  }

  source_details {
    source_type             = "image"
    source_id               = data.oci_core_images.ubuntu.images[0].id
    boot_volume_size_in_gbs = 50
  }

  metadata = {
    ssh_authorized_keys = var.ssh_public_key
    user_data           = base64encode(file("${path.module}/cloud-init.yaml"))
  }
}
