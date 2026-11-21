output "public_ip" {
  value = oci_core_instance.pokehome.public_ip
}

output "ssh_command" {
  value = "ssh -i ~/.ssh/pokehome ubuntu@${oci_core_instance.pokehome.public_ip}"
}

output "image" {
  value = data.oci_core_images.ubuntu.images[0].display_name
}
