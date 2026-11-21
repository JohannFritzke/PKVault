# Servidor PokéHome na Oracle (Always Free)

Esta pasta é um *stack* do Oracle Resource Manager. Ele cria de uma vez só:

- uma rede (VCN, internet gateway e sub-rede pública) com as portas 22, 80 e 443 liberadas;
- uma VM **VM.Standard.E2.1.Micro** (AMD, Always Free) com **Ubuntu 24.04** e disco de 50 GB;
- no primeiro boot (`cloud-init.yaml`): atualizações do sistema, 2 GB de swap e as portas 80/443 abertas no firewall do Ubuntu.

## 1. Gerar a chave SSH (no seu PC, uma vez)

No PowerShell:

```powershell
ssh-keygen -t rsa -b 4096 -f "$env:USERPROFILE\.ssh\pokehome" -C pokehome
```

O comando gera dois arquivos:

- `pokehome` é a chave **privada**. Ela fica só no seu PC e não deve ser enviada a ninguém.
- `pokehome.pub` é a chave **pública**. É ela que vai para a Oracle.

## 2. Criar o stack

1. Gere o zip desta pasta (os arquivos ficam na raiz do zip):
   ```powershell
   Compress-Archive -Path deploy\oracle\* -DestinationPath pokehome-oracle-stack.zip -Force
   ```
2. No painel da Oracle, vá em **☰ → Developer Services → Resource Manager → Stacks → Create stack**.
3. Escolha **My configuration → .Zip file** e envie o zip. Depois clique em **Next**.
4. No formulário:
   - Em **SSH public key**, envie o arquivo `pokehome.pub`.
   - Deixe o resto como está e clique em **Next**.
5. Marque **Run apply** e clique em **Create**.
6. Espere o job ficar **Succeeded**. Ele leva uns 2 a 5 minutos, e a VM ainda precisa de mais alguns minutos para terminar o primeiro boot.
7. Abra a aba **Outputs** do job e anote o `public_ip`.

Se o job falhar com **"Out of host capacity"**, vá em **Edit stack**, troque **Availability domain** para 2 ou 3 e rode o **Apply** de novo.

## 3. Testar o acesso

```powershell
ssh -i "$env:USERPROFILE\.ssh\pokehome" ubuntu@<public_ip>
```

## Apagar tudo

No stack, rode **Destroy**. Isso apaga a VM e a rede.
