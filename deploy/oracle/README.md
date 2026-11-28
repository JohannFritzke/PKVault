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

## Sessões do app (login)

- O login de cada pessoa dura **30 dias** (migração `1795700000_users_token_30_days.js`).
- O app renova o login quando abre e uma vez por dia enquanto está aberto. Só quem fica mais de 30 dias sem abrir o app precisa entrar de novo. Nesse caso, o app mostra "Sua sessão na nuvem expirou" com o botão "Entrar de novo".

### Deslogar todo mundo de uma vez

Use isso se achar que um login vazou.

1. Abra o painel admin do PocketBase (`https://<seu-dominio>/_/`).
2. Vá em **Collections → users → ⚙ (editar a collection) → Options**.
3. Em **Auth token**, use **"Invalidate all previously issued tokens"** (gera um novo segredo).
4. Salve.

Todos os apps passam a mostrar "Sua sessão na nuvem expirou". Cada pessoa entra de novo com a mesma conta e continua de onde parou.

Para barrar **uma** pessoa só, mude o `status` dela para `BLOCKED` em **Collections → users**.

## Apagar tudo

No stack, rode **Destroy**. Isso apaga a VM e a rede.
