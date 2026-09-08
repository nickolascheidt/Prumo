# Ambiente dev na AWS — plano de implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

> **Revisto em 2026-09-08**, quando o Nickolas enquadrou este ambiente como a **versão piloto para testar o mercado**, e não como caixa de teste descartável. Mudou: região `sa-east-1` (Task 0, Passo 3b), snapshot diário do disco (Task 5, Passo 2), domínio deixou de bloquear o primeiro `apply` (Task 8, Passo 0) e o `terraform destroy` deixou de ser rotina (Task 11, item 8).

**Goal:** Pôr o Prumo no ar na AWS em São Paulo, numa única EC2 rodando `docker compose`, com TLS e backup diário, ao ponto de fazer login de um navegador.

**Architecture:** Uma EC2 `t3.small` na VPC default de `sa-east-1` roda quatro containers — Caddy na borda, o nginx do Angular, a API e o Postgres. Uma policy de Data Lifecycle Manager tira snapshot diário do volume raiz. O Terraform novo vive em `terraform/aws/dev/` no repo de DevOps, ao lado da árvore `azurerm`, que não é tocada. As imagens são construídas pelo GitHub Actions e empurradas para o ECR; o deploy é um SSM Run Command. Nenhuma chave de acesso existe: instance profile na máquina, OIDC no CI.

**Tech Stack:** Terraform >= 1.10 (backend S3 com locking nativo), AWS provider ~> 6.0, Amazon Linux 2023, Docker + compose v2, Caddy 2, Postgres 17, .NET 10, Angular.

**Spec:** `docs/superpowers/specs/2026-09-07-aws-dev-environment-design.md`

---

## Antes de começar: três repos

Este plano atravessa três repositórios. Cada task diz em qual ela acontece.

| Apelido | Caminho | O que muda |
|---|---|---|
| **app** | `SaaSBasePlatform` | `appsettings.Production.json`, um teste novo, a workflow de build |
| **angular** | `SaaSBasePlatform-Angular` | só a workflow de build (ACR → ECR) |
| **devops** | `SaaSBasePlatform-DevOps` | tudo o mais: Terraform, compose de deploy, `deploy.yml` |

Os nomes de repo no GitHub ainda são os antigos — o pass de rename para Prumo (`2026-08-11-infra-rename-pass.md`) não rodou. Se ele rodar antes deste plano, ajuste os nomes nas condições de OIDC da Task 5.

## Honestidade sobre verificação

As tasks 2 a 8 e 10 a 11 **exigem uma conta AWS e gastam dinheiro de verdade** (poucos dólares, e `terraform destroy` zera). Não dá para verificá-las offline. É tarefa a dois, como o repo já registra para `az login`.

O primeiro `terraform plan` de cada task é onde a realidade corrige o HCL escrito aqui: este plano foi escrito sem uma conta para validar contra. Espere corrigir nomes de atributo. Isso é normal e não significa que o desenho está errado.

---

## Task 0: Pré-requisitos humanos

**Repo:** nenhum. É trabalho do Nickolas, e bloqueia todo o resto.

- [ ] **Passo 1: Conta AWS com o mínimo de higiene**

Criar a conta. Precisa de cartão de crédito mesmo com créditos — não tem como fugir disso.

**No cadastro a AWS pergunta Free Plan ou Paid Plan. Escolha Paid Plan.** Os US$ 100 de crédito (US$ 200 completando as cinco tarefas de onboarding) são idênticos nos dois; a diferença é que o **Free Plan fecha a sua conta automaticamente** quando os créditos acabam ou em 6 meses, o que vier primeiro. Para um ambiente com cliente de piloto dentro, isso é destruição de dados agendada. O budget alarm do item 3 é a proteção certa contra gastar sem querer; o auto-close não é.

Depois, e antes de qualquer outra coisa:

1. Ativar MFA na conta raiz e **parar de usar a raiz**.
2. Criar um usuário IAM administrativo para o dia a dia, com MFA.
3. Criar um **budget alarm** em Billing: US$ 40/mês, alerta por e-mail em 50% e 100%. (Eram US$ 20 quando o alvo era `us-east-1`; São Paulo custa ~US$ 32/mês ligado 24/7, e um limite abaixo do custo real só ensina a ignorar o alerta.) Criar o budget é, de quebra, uma das cinco tarefas que liberam os US$ 100 extras de crédito.

- [ ] **Passo 2: AWS CLI configurada**

```bash
aws --version
aws configure     # chave do usuário administrativo, região sa-east-1
aws sts get-caller-identity
```

Esperado: um JSON com `Account`, `Arn` terminando no nome do usuário administrativo — **não** em `:root`.

Anote o número da conta. Ele aparece como `<ACCOUNT_ID>` no resto do plano.

- [ ] **Passo 3: Nome de host**

**Este passo deixou de bloquear** na revisão de 2026-09-08. Três caminhos, e o plano cobre os três:

- **Não tem domínio, ou não quer gastar agora:** não faça nada aqui. O ambiente sobe com `sslip.io`, que resolve `<ip-com-hifens>.sslip.io` para o IP sem cadastro nenhum. O Caddy tira certificado válido do Let's Encrypt normalmente. `<DOMINIO>` no resto do plano vira esse nome, e você só o conhece depois da Task 5 (é o IP elástico) — a Task 8 diz onde preenchê-lo.
- **Já tem domínio num registrador** (Registro.br, Namecheap): você vai criar um registro A à mão na Task 8. Deixe `route53_zone_id` vazio.
- **Quer tudo na AWS:** registre ou crie uma hosted zone no Route 53 e anote o **zone ID**.

Sem domínio próprio você chega ao fim deste plano, mas não ao fim do seguinte: **o SES exige domínio verificado com DKIM**, e ninguém mostra `54-207-1-2.sslip.io` para um cliente. Registrar custa ~R$ 40/ano no Registro.br; faça antes de precisar, não depois.

- [ ] **Passo 3b: Confirmar a região**

Este ambiente é **`sa-east-1` (São Paulo)**, decidido em 2026-09-08 — ver "Por que São Paulo" na spec. Todo comando `aws` deste plano assume essa região, e `terraform.tfvars` a fixa.

Não é uma escolha para revisitar depois de aplicar: o Postgres vive no volume EBS da instância, então trocar de região com o piloto no ar é snapshot, cópia entre regiões e recriar tudo.

- [ ] **Passo 4: Terraform 1.10 ou mais novo**

```bash
terraform version
```

Esperado: `Terraform v1.10.0` ou superior. O backend S3 com locking nativo (`use_lockfile`) não existe antes disso, e a alternativa — tabela DynamoDB — está deprecada.

---

## Task 1: Estado remoto e esqueleto do Terraform

**Repo:** devops

**Files:**
- Create: `terraform/aws/dev/versions.tf`
- Create: `terraform/aws/dev/variables.tf`
- Create: `terraform/aws/dev/main.tf`
- Create: `terraform/aws/dev/outputs.tf`
- Create: `terraform/aws/dev/terraform.tfvars`

- [ ] **Passo 1: Criar o bucket de estado pela CLI**

O bucket que guarda o estado não pode ser gerenciado pelo estado que ele guarda. Uma árvore Terraform separada só para isso — que é o que a Azure faz com o `terraform/core` — é mais peça do que este ambiente merece. Três comandos:

```bash
# O --create-bucket-configuration é obrigatório fora de us-east-1: sem ele a AWS
# responde IllegalLocationConstraintException. É a primeira pegadinha de ter
# escolhido São Paulo, e é a única do plano inteiro.
aws s3api create-bucket \
  --bucket prumo-tfstate-<ACCOUNT_ID> \
  --region sa-east-1 \
  --create-bucket-configuration LocationConstraint=sa-east-1

aws s3api put-bucket-versioning \
  --bucket prumo-tfstate-<ACCOUNT_ID> \
  --versioning-configuration Status=Enabled

aws s3api put-public-access-block \
  --bucket prumo-tfstate-<ACCOUNT_ID> \
  --public-access-block-configuration \
  BlockPublicAcls=true,IgnorePublicAcls=true,BlockPublicPolicy=true,RestrictPublicBuckets=true
```

O versionamento não é opcional: é o que permite voltar um estado corrompido.

- [ ] **Passo 2: `versions.tf`**

```terraform
terraform {
  # `use_lockfile` (locking nativo do S3) exige 1.10. A alternativa, tabela
  # DynamoDB, está deprecada e será removida.
  required_version = ">= 1.10.0"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 6.0"
    }
  }

  backend "s3" {
    bucket       = "prumo-tfstate-<ACCOUNT_ID>"
    key          = "aws/dev.tfstate"
    region       = "sa-east-1"
    encrypt      = true
    use_lockfile = true
  }
}

provider "aws" {
  region = var.aws_region

  default_tags {
    tags = {
      Project     = "prumo"
      Environment = "dev"
      ManagedBy   = "terraform"
    }
  }
}
```

Substitua `<ACCOUNT_ID>` pelo número real: o bloco `backend` não aceita variável.

- [ ] **Passo 3: `variables.tf`**

```terraform
variable "aws_region" {
  description = "Região de tudo neste ambiente."
  type        = string
  default     = "sa-east-1"
}

variable "project" {
  description = "Prefixo dos nomes de recurso."
  type        = string
  default     = "prumo"
}

variable "domain" {
  description = "Domínio completo deste ambiente, ex.: dev.seudominio.com."
  type        = string
}

variable "route53_zone_id" {
  description = "Zone ID no Route 53. Vazio significa que o DNS é gerenciado fora da AWS e o registro A é criado à mão."
  type        = string
  default     = ""
}

variable "instance_type" {
  description = "Tamanho da EC2. t3.small são 2 GB para quatro containers; se apertar, t3.medium."
  type        = string
  default     = "t3.small"
}

variable "github_repos" {
  description = "Repos que podem assumir a role de deploy por OIDC, no formato owner/repo."
  type        = list(string)
  default = [
    "nickolascheidt/SaaSBasePlatform",
    "nickolascheidt/SaaSBasePlatform-Angular",
    "nickolascheidt/SaaSBasePlatform-DevOps",
  ]
}
```

- [ ] **Passo 4: `main.tf` com os dados que todo o resto usa**

```terraform
data "aws_caller_identity" "current" {}

data "aws_vpc" "default" {
  default = true
}

data "aws_subnets" "default" {
  filter {
    name   = "vpc-id"
    values = [data.aws_vpc.default.id]
  }
}

# AMI do Amazon Linux 2023 pelo parâmetro público do SSM: nunca fica velha e não
# exige atualizar um ID a cada release.
data "aws_ssm_parameter" "al2023" {
  name = "/aws/service/ami-amazon-linux-latest/al2023-ami-kernel-6.1-x86_64"
}

locals {
  name           = "${var.project}-dev"
  account_id     = data.aws_caller_identity.current.account_id
  ecr_registry   = "${data.aws_caller_identity.current.account_id}.dkr.ecr.${var.aws_region}.amazonaws.com"
  ssm_prefix     = "/${var.project}/dev"
}
```

- [ ] **Passo 5: `outputs.tf`**

```terraform
output "ecr_registry" {
  description = "Host do ECR, usado no docker login e nos nomes de imagem."
  value       = local.ecr_registry
}
```

- [ ] **Passo 6: `terraform.tfvars`**

```terraform
region = "sa-east-1"

# Sem domínio próprio, deixe como está e volte aqui depois da Task 5: o nome vira
# o IP elástico com hífens, ex. "54-207-1-2.sslip.io" (Task 8, Passo 0).
domain = "PREENCHER_NA_TASK_8"

# Preencha com o zone ID se o DNS estiver no Route 53. Vazio = registro A à mão,
# ou nenhum registro, no caminho do sslip.io.
route53_zone_id = ""
```

`domain` é usado pelo `Caddyfile` (Task 6) e pelo `appsettings.Production.json` (Task 9), não por nenhum recurso da AWS — por isso ele pode ficar pendente até a Task 8 sem travar as tasks 1 a 7.

- [ ] **Passo 7: Verificar**

```bash
cd terraform/aws/dev
terraform init
terraform validate
```

Esperado: `Terraform has been successfully initialized!` e `Success! The configuration is valid.`

- [ ] **Passo 8: Commitar**

```bash
git add terraform/aws/dev/
git commit -m "feat(aws): scaffold the dev environment tree with S3 state"
```

---

## Task 2: ECR e as filas SQS

**Repo:** devops

**Files:**
- Create: `terraform/aws/dev/ecr.tf`
- Create: `terraform/aws/dev/sqs.tf`

- [ ] **Passo 1: `ecr.tf`**

```terraform
resource "aws_ecr_repository" "api" {
  name                 = "${var.project}-api"
  image_tag_mutability = "MUTABLE"

  # Sem isto o `terraform destroy` FALHA num repositório que tem imagem dentro, e
  # este ambiente existe para ser destruído e recriado.
  force_delete = true

  image_scanning_configuration {
    scan_on_push = true
  }
}

resource "aws_ecr_repository" "web" {
  name                 = "${var.project}-web"
  image_tag_mutability = "MUTABLE"
  force_delete         = true

  image_scanning_configuration {
    scan_on_push = true
  }
}

# Sem isto o ECR acumula uma imagem por commit para sempre, e você paga por elas.
resource "aws_ecr_lifecycle_policy" "api" {
  repository = aws_ecr_repository.api.name
  policy = jsonencode({
    rules = [{
      rulePriority = 1
      description  = "Guardar as 10 imagens mais recentes"
      selection = {
        tagStatus   = "any"
        countType   = "imageCountMoreThan"
        countNumber = 10
      }
      action = { type = "expire" }
    }]
  })
}

resource "aws_ecr_lifecycle_policy" "web" {
  repository = aws_ecr_repository.web.name
  policy     = aws_ecr_lifecycle_policy.api.policy
}
```

- [ ] **Passo 2: `sqs.tf`**

```terraform
# Os números são os mesmos do elasticmq/elasticmq.conf no repo da aplicação, de
# propósito: o local não deve ser mais permissivo que o remoto.
resource "aws_sqs_queue" "notifications_dlq" {
  name = "${local.name}-notifications-dlq"
}

resource "aws_sqs_queue" "notifications" {
  name                       = "${local.name}-notifications"
  visibility_timeout_seconds = 60
  receive_wait_time_seconds  = 20

  redrive_policy = jsonencode({
    deadLetterTargetArn = aws_sqs_queue.notifications_dlq.arn
    maxReceiveCount     = 5
  })
}
```

- [ ] **Passo 3: Acrescentar as saídas em `outputs.tf`**

```terraform
output "queue_url" {
  description = "Vai para Sqs__QueueUrl no .env da instância."
  value       = aws_sqs_queue.notifications.url
}

output "dead_letter_queue_url" {
  description = "Vai para Sqs__DeadLetterQueueUrl quando o worker existir."
  value       = aws_sqs_queue.notifications_dlq.url
}
```

- [ ] **Passo 4: Aplicar**

```bash
terraform apply
```

Esperado: 6 recursos criados.

- [ ] **Passo 5: Verificar que o redrive está mesmo lá**

```bash
aws sqs get-queue-attributes \
  --queue-url "$(terraform output -raw queue_url)" \
  --attribute-names RedrivePolicy VisibilityTimeout ReceiveMessageWaitTimeSeconds
```

Esperado: `RedrivePolicy` com `"maxReceiveCount":5` apontando para o ARN da DLQ, `VisibilityTimeout` `60`, `ReceiveMessageWaitTimeSeconds` `20`. Se o redrive vier vazio, a fila envenenada não teria para onde ir — não siga.

- [ ] **Passo 6: Commitar**

```bash
git add terraform/aws/dev/ecr.tf terraform/aws/dev/sqs.tf terraform/aws/dev/outputs.tf
git commit -m "feat(aws): create the ECR repositories and the notification queues"
```

---

## Task 3: Segredos no SSM Parameter Store

**Repo:** devops

**Files:**
- Create: `terraform/aws/dev/ssm.tf`

O Terraform **declara** os parâmetros; os valores entram à mão. Segredo em variável de Terraform acaba em texto claro no state file, que é exatamente o que o Key Vault evitava na Azure.

- [ ] **Passo 1: `ssm.tf`**

```terraform
locals {
  secret_names = [
    "jwt-key",
    "seed-admin-password",
    "postgres-password",
    "prumo-app-password",
    "prumo-migrator-password",
  ]
}

resource "aws_ssm_parameter" "secrets" {
  for_each = toset(local.secret_names)

  name  = "${local.ssm_prefix}/${each.value}"
  type  = "SecureString"
  value = "PREENCHER"

  # O valor real entra por `aws ssm put-parameter --overwrite`, nunca pelo
  # Terraform: valor em variável vira texto claro no state file.
  lifecycle {
    ignore_changes = [value]
  }
}
```

- [ ] **Passo 2: Aplicar**

```bash
terraform apply
```

Esperado: 5 parâmetros criados, todos com o valor `PREENCHER`.

- [ ] **Passo 3: Pôr os valores de verdade**

Gere segredos fortes e distintos. Um exemplo por parâmetro:

```bash
PREFIX=/prumo/dev

aws ssm put-parameter --name "$PREFIX/jwt-key" --type SecureString --overwrite \
  --value "$(openssl rand -base64 48)"

aws ssm put-parameter --name "$PREFIX/postgres-password" --type SecureString --overwrite \
  --value "$(openssl rand -base64 24 | tr -d '/+=')"

aws ssm put-parameter --name "$PREFIX/prumo-app-password" --type SecureString --overwrite \
  --value "$(openssl rand -base64 24 | tr -d '/+=')"

aws ssm put-parameter --name "$PREFIX/prumo-migrator-password" --type SecureString --overwrite \
  --value "$(openssl rand -base64 24 | tr -d '/+=')"
```

A senha do admin semeado precisa passar na política do Identity (mínimo 6, com dígito, minúscula e maiúscula — ver `DatabaseConfiguration.cs`), então escolha uma à mão em vez de gerar:

```bash
aws ssm put-parameter --name "$PREFIX/seed-admin-password" --type SecureString --overwrite \
  --value 'TrocarDepois1'
```

O `tr -d '/+='` existe porque esses caracteres quebram a connection string do Postgres quando entram na senha sem escape.

- [ ] **Passo 4: Verificar que nenhum ficou em `PREENCHER`**

```bash
aws ssm get-parameters-by-path --path /prumo/dev --with-decryption \
  --query 'Parameters[?Value==`PREENCHER`].Name' --output text
```

Esperado: **saída vazia**. Qualquer nome listado é um segredo que não foi preenchido.

- [ ] **Passo 5: Commitar**

```bash
git add terraform/aws/dev/ssm.tf
git commit -m "feat(aws): declare the SSM parameters that hold the environment secrets"
```

---

## Task 4: OIDC do GitHub e a role de deploy

**Repo:** devops

**Files:**
- Create: `terraform/aws/dev/oidc.tf`

Substitui as federated credentials do Entra. Nenhuma chave de acesso vai para os secrets do GitHub.

- [ ] **Passo 1: `oidc.tf`**

```terraform
# Sem `thumbprint_list`: para GitHub, GitLab, Google e Auth0 a AWS valida pela
# própria biblioteca de CAs raiz e ignora thumbprint configurado.
resource "aws_iam_openid_connect_provider" "github" {
  url             = "https://token.actions.githubusercontent.com"
  client_id_list  = ["sts.amazonaws.com"]
}

data "aws_iam_policy_document" "github_assume" {
  statement {
    effect  = "Allow"
    actions = ["sts:AssumeRoleWithWebIdentity"]

    principals {
      type        = "Federated"
      identifiers = [aws_iam_openid_connect_provider.github.arn]
    }

    condition {
      test     = "StringEquals"
      variable = "token.actions.githubusercontent.com:aud"
      values   = ["sts.amazonaws.com"]
    }

    # StringLike, e não StringEquals: o `sub` carrega a ref do commit, então o
    # sufixo varia a cada execução.
    condition {
      test     = "StringLike"
      variable = "token.actions.githubusercontent.com:sub"
      values   = [for r in var.github_repos : "repo:${r}:*"]
    }
  }
}

resource "aws_iam_role" "github_deploy" {
  name               = "${local.name}-github-deploy"
  assume_role_policy = data.aws_iam_policy_document.github_assume.json
}

data "aws_iam_policy_document" "github_deploy" {
  # Empurrar imagem para os dois repositórios do ECR.
  statement {
    effect = "Allow"
    actions = [
      "ecr:BatchCheckLayerAvailability",
      "ecr:CompleteLayerUpload",
      "ecr:InitiateLayerUpload",
      "ecr:PutImage",
      "ecr:UploadLayerPart",
      "ecr:BatchGetImage",
      "ecr:GetDownloadUrlForLayer",
    ]
    resources = [
      aws_ecr_repository.api.arn,
      aws_ecr_repository.web.arn,
    ]
  }

  # O token de login do ECR não aceita recurso específico.
  statement {
    effect    = "Allow"
    actions   = ["ecr:GetAuthorizationToken"]
    resources = ["*"]
  }

  # Mandar o comando de deploy para a instância.
  statement {
    effect  = "Allow"
    actions = ["ssm:SendCommand"]
    resources = [
      aws_instance.app.arn,
      "arn:aws:ssm:${var.aws_region}::document/AWS-RunShellScript",
    ]
  }

  statement {
    effect    = "Allow"
    actions   = ["ssm:GetCommandInvocation", "ssm:ListCommandInvocations"]
    resources = ["*"]
  }
}

resource "aws_iam_role_policy" "github_deploy" {
  name   = "deploy"
  role   = aws_iam_role.github_deploy.id
  policy = data.aws_iam_policy_document.github_deploy.json
}
```

- [ ] **Passo 2: Acrescentar a saída em `outputs.tf`**

```terraform
output "github_deploy_role_arn" {
  description = "Vai para o secret AWS_DEPLOY_ROLE_ARN nos três repos."
  value       = aws_iam_role.github_deploy.arn
}
```

**Nota:** esta task referencia `aws_instance.app`, que só nasce na Task 5. Aplique as duas juntas — rode a Task 5 antes do `terraform apply`.

- [ ] **Passo 3: Commitar (sem aplicar ainda)**

```bash
git add terraform/aws/dev/oidc.tf terraform/aws/dev/outputs.tf
git commit -m "feat(aws): trust GitHub Actions through OIDC for deploys"
```

---

## Task 5: A instância, o security group e o instance profile

**Repo:** devops

**Files:**
- Create: `terraform/aws/dev/instance.tf`
- Create: `terraform/aws/dev/backup.tf`
- Create: `terraform/aws/dev/user-data.sh`

- [ ] **Passo 1: `instance.tf`**

```terraform
resource "aws_security_group" "app" {
  name        = "${local.name}-app"
  description = "HTTP e HTTPS da internet. Sem SSH: o acesso é por SSM Session Manager."
  vpc_id      = data.aws_vpc.default.id

  ingress {
    description = "HTTP (redireciona para HTTPS e serve o desafio do Let's Encrypt)"
    from_port   = 80
    to_port     = 80
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  ingress {
    description = "HTTPS"
    from_port   = 443
    to_port     = 443
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  # Saída aberta: o compose precisa puxar do ECR, do Docker Hub e do Let's Encrypt.
  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }
}

data "aws_iam_policy_document" "ec2_assume" {
  statement {
    effect  = "Allow"
    actions = ["sts:AssumeRole"]

    principals {
      type        = "Service"
      identifiers = ["ec2.amazonaws.com"]
    }
  }
}

resource "aws_iam_role" "app" {
  name               = "${local.name}-instance"
  assume_role_policy = data.aws_iam_policy_document.ec2_assume.json
}

# Habilita SSM Session Manager e Run Command. É o que substitui a porta 22.
resource "aws_iam_role_policy_attachment" "ssm_core" {
  role       = aws_iam_role.app.name
  policy_arn = "arn:aws:iam::aws:policy/AmazonSSMManagedInstanceCore"
}

data "aws_iam_policy_document" "app" {
  statement {
    effect    = "Allow"
    actions   = ["sqs:SendMessage"]
    resources = [aws_sqs_queue.notifications.arn]
  }

  statement {
    effect    = "Allow"
    actions   = ["ssm:GetParameter", "ssm:GetParameters", "ssm:GetParametersByPath"]
    resources = ["arn:aws:ssm:${var.aws_region}:${local.account_id}:parameter${local.ssm_prefix}/*"]
  }

  statement {
    effect = "Allow"
    actions = [
      "ecr:BatchCheckLayerAvailability",
      "ecr:BatchGetImage",
      "ecr:GetDownloadUrlForLayer",
    ]
    resources = [
      aws_ecr_repository.api.arn,
      aws_ecr_repository.web.arn,
    ]
  }

  statement {
    effect    = "Allow"
    actions   = ["ecr:GetAuthorizationToken"]
    resources = ["*"]
  }
}

resource "aws_iam_role_policy" "app" {
  name   = "app"
  role   = aws_iam_role.app.id
  policy = data.aws_iam_policy_document.app.json
}

resource "aws_iam_instance_profile" "app" {
  name = "${local.name}-instance"
  role = aws_iam_role.app.name
}

resource "aws_instance" "app" {
  ami                    = data.aws_ssm_parameter.al2023.value
  instance_type          = var.instance_type
  subnet_id              = data.aws_subnets.default.ids[0]
  vpc_security_group_ids = [aws_security_group.app.id]
  iam_instance_profile   = aws_iam_instance_profile.app.name

  root_block_device {
    volume_size = 30
    volume_type = "gp3"
    encrypted   = true
  }

  user_data                   = local.user_data
  user_data_replace_on_change = true

  tags = { Name = local.name }
}

# IP fixo: sem ele o registro DNS quebraria a cada recriação da instância.
resource "aws_eip" "app" {
  instance = aws_instance.app.id
  domain   = "vpc"
}
```

Repare na tag `Name = local.name` da instância: é por ela que a policy de backup do Passo 2 encontra o volume. Trocar a tag sem trocar a policy desliga o backup em silêncio.

- [ ] **Passo 2: `backup.tf` — o snapshot diário**

Decisão 10 da spec. O banco do piloto vive no volume raiz desta instância; isto é a única coisa entre um `terraform destroy` acidental e perder tudo.

```terraform
# O DLM precisa de uma role própria: quem tira o snapshot é o serviço, não você.
data "aws_iam_policy_document" "dlm_assume" {
  statement {
    actions = ["sts:AssumeRole"]
    principals {
      type        = "Service"
      identifiers = ["dlm.amazonaws.com"]
    }
  }
}

resource "aws_iam_role" "dlm" {
  name               = "${local.name}-dlm"
  assume_role_policy = data.aws_iam_policy_document.dlm_assume.json
}

resource "aws_iam_role_policy_attachment" "dlm" {
  role       = aws_iam_role.dlm.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AWSDataLifecycleManagerServiceRole"
}

resource "aws_dlm_lifecycle_policy" "daily" {
  description        = "${local.name}: snapshot diario do volume raiz"
  execution_role_arn = aws_iam_role.dlm.arn
  state              = "ENABLED"

  policy_details {
    resource_types = ["INSTANCE"]

    # Casa com a tag Name da instância criada no Passo 1.
    target_tags = { Name = local.name }

    schedule {
      name = "diario-7-dias"

      create_rule {
        # 06:00 UTC = 03:00 em Brasília, quando ninguém está usando o piloto.
        cron_expression = "cron(0 6 * * ? *)"
      }

      retain_rule {
        count = 7
      }

      # Sem isto o snapshot não herda a tag Name e fica impossível saber de quem é.
      copy_tags = true
    }
  }
}
```

Nota honesta, que também está na spec: isto é backup **a frio**, do volume, com o Postgres escrevendo. Ele recupera, mas a restauração pode passar por recuperação de crash no boot do banco. `pg_dump` para o S3 é o passo seguinte, e não está nesta fatia.

- [ ] **Passo 3: Aplicar as tasks 4 e 5 juntas**

```bash
terraform apply
```

Esperado: o security group, as roles, o instance profile, a instância, o EIP, a role e a policy do DLM, o OIDC provider e a role de deploy. O `user_data` ainda não existe — a Task 7 o cria; por ora, comente a linha `user_data = local.user_data` para este primeiro apply e descomente lá.

- [ ] **Passo 4: Verificar que o SSM enxerga a instância**

O agente leva ~2 minutos para registrar depois do boot.

```bash
aws ssm describe-instance-information \
  --query 'InstanceInformationList[].{Id:InstanceId,Ping:PingStatus}' --output table
```

Esperado: a instância listada com `PingStatus` `Online`. Se ficar vazio, o instance profile não subiu junto — confira a policy `AmazonSSMManagedInstanceCore`.

- [ ] **Passo 5: Entrar na máquina sem SSH**

```bash
aws ssm start-session --target <INSTANCE_ID>
```

Esperado: um shell. Isto prova que não há motivo para abrir a porta 22.

- [ ] **Passo 6: Confirmar que a policy de backup está ativa**

```bash
aws dlm get-lifecycle-policies \
  --query 'Policies[].{Id:PolicyId,State:State}' --output table
```

Esperado: uma policy com `State` = `ENABLED`. Isto só prova que ela existe — **que ela realmente tira snapshot se verifica no dia seguinte**, na Task 11.

- [ ] **Passo 7: Commitar**

```bash
git add terraform/aws/dev/instance.tf terraform/aws/dev/backup.tf
git commit -m "feat(aws): create the instance, its profile, the security group and the daily snapshot"
```

---

## Task 6: Os arquivos de deploy

**Repo:** devops

**Files:**
- Create: `deploy/docker-compose.yml`
- Create: `deploy/Caddyfile`
- Create: `deploy/db/roles.sql`

Este compose **não é** o `docker-compose.yml` da raiz do repo da aplicação. Aquele é de desenvolvimento, monta código local e sobe o ElasticMQ; este puxa imagens do ECR e fala com o SQS real.

- [ ] **Passo 1: Copiar `db/roles.sql` do repo da aplicação**

```bash
cp ../SaaSBasePlatform/db/roles.sql deploy/db/roles.sql
```

Acrescente no topo do arquivo copiado:

```sql
-- CÓPIA de db/roles.sql do repo SaaSBasePlatform. As duas precisam andar juntas:
-- se lá mudar, aqui muda. Não edite este arquivo; edite o de lá e copie de novo.
```

Isto é duplicação e vai derivar um dia. Está registrado como risco conhecido no fim do plano; a alternativa (assar o script na imagem da API) custa mais do que resolve nesta fatia.

- [ ] **Passo 2: `deploy/Caddyfile`**

```
{$DOMAIN} {
	# O Caddy tira e renova o certificado sozinho pelo desafio HTTP do Let's
	# Encrypt, que é o motivo de a porta 80 estar aberta.
	reverse_proxy web:80
}
```

O nginx do Angular continua fazendo o proxy de `/api` para a API, então o Caddy só precisa conhecer o `web`.

- [ ] **Passo 3: `deploy/docker-compose.yml`**

```yaml
services:
  caddy:
    image: caddy:2-alpine
    restart: unless-stopped
    ports:
      - "80:80"
      - "443:443"
    environment:
      DOMAIN: ${DOMAIN}
    volumes:
      - ./Caddyfile:/etc/caddy/Caddyfile:ro
      # Preserva o certificado entre subidas. Destruir este volume e reaplicar
      # muitas vezes no mesmo dia esbarra no limite de emissão do Let's Encrypt.
      - caddy-data:/data
      - caddy-config:/config
    depends_on:
      - web

  web:
    image: ${ECR_REGISTRY}/prumo-web:${WEB_TAG}
    restart: unless-stopped
    environment:
      # Lido pelo envsubst do entrypoint do nginx, no `proxy_pass ${API_URL};`.
      API_URL: http://api:8080

  api:
    image: ${ECR_REGISTRY}/prumo-api:${API_TAG}
    restart: unless-stopped
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_HTTP_PORTS: 8080
      AllowedHosts: ${DOMAIN}
      App__BaseUrl: https://${DOMAIN}
      Cors__AllowedOrigins__0: https://${DOMAIN}
      ConnectionStrings__DefaultConnection: Host=postgres;Port=5432;Database=SaaSBasePlatformDb;Username=prumo_app;Password=${PRUMO_APP_PASSWORD}
      ConnectionStrings__MigratorConnection: Host=postgres;Port=5432;Database=SaaSBasePlatformDb;Username=prumo_migrator;Password=${PRUMO_MIGRATOR_PASSWORD}
      # Ligado nesta fatia porque não há passo de migration no deploy ainda.
      # Ver a spec: é a peça a substituir quando o compute mudar.
      Database__MigrateOnStartup: "true"
      Notifications__Provider: Sqs
      Sqs__QueueUrl: ${SQS_QUEUE_URL}
      Sqs__Region: ${AWS_REGION}
      # Sqs__ServiceUrl fica AUSENTE de propósito. Preenchida, ela apontaria o SDK
      # para o emulador e trocaria a credencial do instance profile por uma fixa —
      # e a guarda de NotificationConfiguration recusa o startup.
      Jwt__Key: ${JWT_KEY}
      Seed__AdminPassword: ${SEED_ADMIN_PASSWORD}
    depends_on:
      postgres:
        condition: service_healthy

  postgres:
    image: postgres:17-alpine
    restart: unless-stopped
    environment:
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
      POSTGRES_DB: SaaSBasePlatformDb
      PRUMO_MIGRATOR_PASSWORD: ${PRUMO_MIGRATOR_PASSWORD}
      PRUMO_APP_PASSWORD: ${PRUMO_APP_PASSWORD}
    volumes:
      - postgres-data:/var/lib/postgresql/data
      # Só roda em volume novo, igual ao local.
      - ./db/roles.sql:/docker-entrypoint-initdb.d/10-roles.sql:ro
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres"]
      interval: 10s
      timeout: 5s
      retries: 5

volumes:
  postgres-data:
  caddy-data:
  caddy-config:
```

Repare que **nenhuma porta do Postgres é publicada**: ele só existe na rede do compose.

- [ ] **Passo 4: Verificar que o compose é válido**

Da sua máquina, com um `.env` de mentira só para a substituição de variável:

```bash
cd deploy
cat > .env <<'EOF'
DOMAIN=exemplo.com
ECR_REGISTRY=000000000000.dkr.ecr.sa-east-1.amazonaws.com
API_TAG=teste
WEB_TAG=teste
AWS_REGION=sa-east-1
SQS_QUEUE_URL=https://sqs.sa-east-1.amazonaws.com/000000000000/fila
JWT_KEY=x
SEED_ADMIN_PASSWORD=x
POSTGRES_PASSWORD=x
PRUMO_APP_PASSWORD=x
PRUMO_MIGRATOR_PASSWORD=x
EOF
docker compose config > /dev/null && echo OK
rm .env
```

Esperado: `OK`. Qualquer variável não substituída aparece como aviso aqui, e não em produção.

- [ ] **Passo 5: Commitar**

```bash
git add deploy/
git commit -m "feat(deploy): add the compose file, Caddyfile and roles script for the instance"
```

---

## Task 7: O user-data que monta a máquina sozinha

**Repo:** devops

**Files:**
- Create: `terraform/aws/dev/user-data.sh`
- Modify: `terraform/aws/dev/instance.tf`

- [ ] **Passo 1: `user-data.sh`**

```bash
#!/bin/bash
set -euxo pipefail

REGION="${region}"
SSM_PREFIX="${ssm_prefix}"
ECR_REGISTRY="${ecr_registry}"
DOMAIN="${domain}"
QUEUE_URL="${queue_url}"

# --- Docker ---
dnf install -y docker git
systemctl enable --now docker

# O AL2023 não empacota o plugin compose v2; ele vem do release do Docker.
mkdir -p /usr/local/lib/docker/cli-plugins
curl -fsSL \
  "https://github.com/docker/compose/releases/download/v2.29.7/docker-compose-linux-x86_64" \
  -o /usr/local/lib/docker/cli-plugins/docker-compose
chmod +x /usr/local/lib/docker/cli-plugins/docker-compose

# --- arquivos de deploy ---
# Vêm de um tarball no S3? Não: vêm do próprio repo, clonado sem credencial por
# ser leitura pública do artefato de deploy. Enquanto o repo for privado, o
# tarball é enviado pelo comando de deploy (Task 10) e o boot inicial usa o que
# o Terraform já escreveu abaixo.
mkdir -p /opt/prumo
cd /opt/prumo

# --- segredos ---
get() { aws ssm get-parameter --region "$REGION" --name "$SSM_PREFIX/$1" --with-decryption --query Parameter.Value --output text; }

cat > /opt/prumo/.env <<EOF
DOMAIN=$DOMAIN
ECR_REGISTRY=$ECR_REGISTRY
AWS_REGION=$REGION
SQS_QUEUE_URL=$QUEUE_URL
API_TAG=latest
WEB_TAG=latest
JWT_KEY=$(get jwt-key)
SEED_ADMIN_PASSWORD=$(get seed-admin-password)
POSTGRES_PASSWORD=$(get postgres-password)
PRUMO_APP_PASSWORD=$(get prumo-app-password)
PRUMO_MIGRATOR_PASSWORD=$(get prumo-migrator-password)
EOF
chmod 600 /opt/prumo/.env

# --- ECR ---
aws ecr get-login-password --region "$REGION" \
  | docker login --username AWS --password-stdin "$ECR_REGISTRY"

# Se os arquivos de deploy já estiverem em /opt/prumo, sobe. No primeiro boot,
# antes do primeiro deploy, eles ainda não estão — e isso não é erro.
if [ -f /opt/prumo/docker-compose.yml ]; then
  docker compose -f /opt/prumo/docker-compose.yml up -d
fi
```

- [ ] **Passo 2: Ligar o user-data em `instance.tf`**

Acrescente ao bloco `locals` em `main.tf`:

```terraform
locals {
  user_data = templatefile("${path.module}/user-data.sh", {
    region       = var.aws_region
    ssm_prefix   = local.ssm_prefix
    ecr_registry = local.ecr_registry
    domain       = var.domain
    queue_url    = aws_sqs_queue.notifications.url
  })
}
```

E descomente `user_data = local.user_data` em `aws_instance.app`.

Cuidado com o `templatefile`: ele usa `${...}` para as próprias variáveis, e o script tem `$VAR` de shell. As variáveis de shell precisam continuar com `$` simples e as do template com `${}` — o script acima já está escrito assim.

- [ ] **Passo 3: Aplicar e deixar a instância ser recriada**

```bash
terraform apply
```

`user_data_replace_on_change = true` faz o Terraform recriar a instância quando o script muda, em vez de deixar a máquina divergindo do que está escrito.

- [ ] **Passo 4: Verificar o boot**

```bash
aws ssm start-session --target <INSTANCE_ID>
# dentro da instância:
sudo cat /var/log/cloud-init-output.log | tail -40
sudo docker compose version
sudo ls -l /opt/prumo/.env
```

Esperado: o log sem erro, `Docker Compose version v2.29.7`, e o `.env` existindo com permissão `600`. Confira também que ele **não** contém a palavra `PREENCHER`:

```bash
sudo grep -c PREENCHER /opt/prumo/.env
```

Esperado: `0`.

- [ ] **Passo 5: Commitar**

```bash
git add terraform/aws/dev/user-data.sh terraform/aws/dev/main.tf terraform/aws/dev/instance.tf
git commit -m "feat(aws): bootstrap docker and the environment file from user-data"
```

---

## Task 8: DNS e TLS

**Repo:** devops

**Files:**
- Create: `terraform/aws/dev/dns.tf`

> **Se você não tem domínio** (decisão 11, 2026-09-08): faça só o Passo 0 e pule
> direto para a Task 9. `dns.tf` continua sendo criado — com `route53_zone_id`
> vazio ele não gera recurso nenhum — e a Task 8 inteira volta a valer no dia em
> que o domínio existir.

- [ ] **Passo 0: O caminho sem domínio, com `sslip.io`**

```bash
terraform output public_ip     # ex.: 54.207.1.2
```

O nome de host do ambiente é esse IP com hífens no lugar dos pontos, mais `.sslip.io`:

```
54.207.1.2  ->  54-207-1-2.sslip.io
```

Não há cadastro, não há DNS para configurar, não há custo: o `sslip.io` já resolve qualquer nome nesse formato. Como o IP é elástico, ele não muda entre recriações da instância.

Confirme antes de deixar o Caddy tentar:

```bash
dig +short 54-207-1-2.sslip.io
```

Esperado: exatamente o IP do `terraform output public_ip`.

Esse nome é o `<DOMINIO>` do resto do plano: vai no `Caddyfile` (Task 6) e no `appsettings.Production.json` (Task 9). Trocar pelo domínio de verdade depois é editar essas duas linhas e reaplicar o deploy — nenhum recurso da AWS muda.

- [ ] **Passo 1: `dns.tf`**

```terraform
# Só existe quando o DNS está no Route 53. Com route53_zone_id vazio, o registro
# A é criado à mão no registrador — ver o Passo 2.
resource "aws_route53_record" "app" {
  count = var.route53_zone_id == "" ? 0 : 1

  zone_id = var.route53_zone_id
  name    = var.domain
  type    = "A"
  ttl     = 300
  records = [aws_eip.app.public_ip]
}
```

- [ ] **Passo 2: Acrescentar a saída e aplicar**

Em `outputs.tf`:

```terraform
output "public_ip" {
  description = "Aponte o registro A do domínio para cá se o DNS não estiver no Route 53."
  value       = aws_eip.app.public_ip
}
```

```bash
terraform apply
terraform output public_ip
```

Se `route53_zone_id` estiver vazio, crie no seu registrador um registro **A** de `<DOMINIO>` para esse IP e espere a propagação.

- [ ] **Passo 3: Verificar que o DNS resolve antes de deixar o Caddy tentar**

```bash
dig +short <DOMINIO>
```

Esperado: o mesmo IP do `terraform output public_ip`. **Não siga antes disso bater** — o Caddy tentaria emitir o certificado, falharia, e tentativas repetidas consomem o limite do Let's Encrypt.

- [ ] **Passo 4: Commitar**

```bash
git add terraform/aws/dev/dns.tf terraform/aws/dev/outputs.tf
git commit -m "feat(aws): point the domain at the instance"
```

---

## Task 9: Tirar os placeholders de Azure do overlay de produção

**Repo:** app

**Files:**
- Modify: `Prumo.Api/appsettings.Production.json`
- Create: `Prumo.Tests/Architecture/ProductionConfigTests.cs`

Esta é a única task com teste automatizado de verdade, e ela segue o padrão do `TenantCoverageTests`: um teste de arquitetura que **quebra o build** quando a configuração de produção volta a mentir.

- [ ] **Passo 1: Escrever o teste que falha**

Criar `Prumo.Tests/Architecture/ProductionConfigTests.cs`:

```csharp
using System.Text.Json;

namespace Prumo.Tests.Architecture;

/// <summary>
/// O overlay de produção carregou placeholders de Azure por meses — a connection
/// string dizia CONFIGURE_VIA_AZURE_APP_SETTINGS_OR_KEY_VAULT e o AllowedHosts
/// dizia yourdomain.com —, e o deploy os contornava com variável de ambiente.
/// Contornar em silêncio é como eles sobreviveram tanto tempo.
///
/// Este teste quebra o build se algum voltar. Ele NÃO exige que o arquivo tenha o
/// valor real: em produção a connection string continua vindo do ambiente, e o
/// placeholder que quebra o startup de propósito continua sendo o certo — ele só
/// não pode nomear uma nuvem que não usamos mais.
/// </summary>
public class ProductionConfigTests
{
    private static readonly string[] Forbidden =
    [
        "AZURE",
        "yourdomain.com",
    ];

    private static string ProductionConfigPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Prumo.slnx")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "Prumo.Api", "appsettings.Production.json");
    }

    [Fact]
    public void O_overlay_de_producao_nao_carrega_placeholder_de_azure()
    {
        var content = File.ReadAllText(ProductionConfigPath());

        foreach (var term in Forbidden)
        {
            Assert.DoesNotContain(term, content, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// A falha segura que já existe e deve continuar existindo: sem a env var
    /// ConnectionStrings__DefaultConnection, o startup quebra em vez de cair num
    /// banco default.
    /// </summary>
    [Fact]
    public void O_overlay_de_producao_mantem_a_connection_string_como_placeholder()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(ProductionConfigPath()));

        var value = doc.RootElement
            .GetProperty("ConnectionStrings")
            .GetProperty("DefaultConnection")
            .GetString();

        Assert.NotNull(value);
        Assert.DoesNotContain("Host=", value!, StringComparison.OrdinalIgnoreCase);
    }
}
```

- [ ] **Passo 2: Rodar e ver falhar**

```bash
dotnet test --filter "FullyQualifiedName~ProductionConfigTests"
```

Esperado: `O_overlay_de_producao_nao_carrega_placeholder_de_azure` **falha** — o arquivo contém `CONFIGURE_VIA_AZURE_APP_SETTINGS_OR_KEY_VAULT` e `yourdomain.com`. O segundo teste passa.

- [ ] **Passo 3: Corrigir o overlay**

Em `Prumo.Api/appsettings.Production.json`, trocar as três coisas. O `AllowedHosts` e o CORS passam a vir do ambiente (o compose os põe), então o arquivo deixa de fingir que os conhece:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "CONFIGURE_VIA_ENVIRONMENT"
  },
  "Database": {
    "MigrateOnStartup": false
  },
  "Jwt": {
    "Issuer": "PrumoApi",
    "Audience": "PrumoClient"
  },
  "Cors": {
    "AllowedOrigins": []
  },
  "RateLimiting": {
    "PermitLimit": 60,
    "Window": 60,
    "QueueLimit": 5
  },
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Error"
    }
  },
  "AllowedHosts": ""
}
```

- [ ] **Passo 4: Trocar a região default de `us-east-1` para `sa-east-1`**

O deploy manda `Sqs__Region` por env var, então isto não muda comportamento nenhum no ambiente — muda o que acontece quando a env var **falta**, que é o caso do desenvolvedor rodando local. Um default apontando para a Virgínia enquanto tudo vive em São Paulo é uma pegadinha esperando alguém.

Quatro lugares, todos com o mesmo valor:

```bash
# Os dois appsettings.json (chave Sqs:Region):
#   Prumo.Api/appsettings.json
#   Prumo.Notifications/appsettings.json
# E os dois fallbacks no código:
#   Prumo.Api/Configuration/NotificationConfiguration.cs:115
#   Prumo.Notifications/Program.cs:130
grep -rn '"us-east-1"\|us-east-1' --include=*.json --include=*.cs \
  Prumo.Api Prumo.Notifications
```

Os `us-east-1` que sobram em `Prumo.Tests/Api/NotificationConfigurationTests.cs` são URLs de fila fabricadas para o teste; não têm nada a ver com a região real e podem ficar.

- [ ] **Passo 5: Rodar e ver passar**

```bash
dotnet test
```

Esperado: **138 aprovados**, 0 falhas (136 de hoje mais os 2 novos).

- [ ] **Passo 6: Commitar**

```bash
git add Prumo.Api/appsettings.Production.json Prumo.Api/appsettings.json \
        Prumo.Api/Configuration/NotificationConfiguration.cs \
        Prumo.Notifications/appsettings.json Prumo.Notifications/Program.cs \
        Prumo.Tests/Architecture/ProductionConfigTests.cs
git commit -m "fix(config): stop naming Azure in the production overlay and default to sa-east-1"
```

---

## Task 10: Build para o ECR e deploy por SSM

**Repos:** app, angular, devops

**Files:**
- Modify: `.github/workflows/*.yml` no repo **app** (o que hoje empurra para o ACR)
- Modify: `.github/workflows/*.yml` no repo **angular**
- Modify: `.github/workflows/deploy.yml` no repo **devops**

- [ ] **Passo 1: Pôr os secrets nos três repos**

```bash
cd terraform/aws/dev
terraform output -raw github_deploy_role_arn
terraform output -raw ecr_registry
```

Em cada um dos três repos no GitHub, criar dois secrets: `AWS_DEPLOY_ROLE_ARN` e `ECR_REGISTRY` com esses valores. Os secrets antigos do Azure (`ACR_LOGIN_SERVER`, credenciais do Entra) ficam onde estão até a limpeza final — não custam nada parados.

O `DEVOPS_DISPATCH_TOKEN`, usado no `repository_dispatch` dos passos 2 e 3, **já existe** nos repos app e angular: é o mesmo que acordava o devops na Azure, e o mecanismo de dispatch não muda. Confirme que ele ainda está lá antes de seguir:

```bash
gh secret list --repo nickolascheidt/SaaSBasePlatform | grep DEVOPS_DISPATCH_TOKEN
gh secret list --repo nickolascheidt/SaaSBasePlatform-Angular | grep DEVOPS_DISPATCH_TOKEN
```

Esperado: uma linha em cada. Se faltar, crie um PAT com escopo `repo` e adicione com esse nome.

- [ ] **Passo 2: Trocar o push do ACR pelo ECR no repo app**

O job de build passa a ser:

```yaml
permissions:
  id-token: write
  contents: read

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: aws-actions/configure-aws-credentials@v4
        with:
          role-to-assume: ${{ secrets.AWS_DEPLOY_ROLE_ARN }}
          aws-region: sa-east-1

      - uses: aws-actions/amazon-ecr-login@v2

      - name: Build and push
        run: |
          IMAGE="${{ secrets.ECR_REGISTRY }}/prumo-api"
          docker build -t "$IMAGE:${{ github.sha }}" -t "$IMAGE:latest" .
          docker push "$IMAGE:${{ github.sha }}"
          docker push "$IMAGE:latest"

      - name: Dispatch deploy
        uses: peter-evans/repository-dispatch@v3
        with:
          token: ${{ secrets.DEVOPS_DISPATCH_TOKEN }}
          repository: nickolascheidt/SaaSBasePlatform-DevOps
          event-type: deploy
          client-payload: '{"app":"api","image_tag":"${{ github.sha }}"}'
```

`permissions: id-token: write` não é opcional: sem ele o OIDC não emite token e o `configure-aws-credentials` falha com uma mensagem que não explica isso.

- [ ] **Passo 3: O mesmo no repo angular**

```yaml
permissions:
  id-token: write
  contents: read

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: aws-actions/configure-aws-credentials@v4
        with:
          role-to-assume: ${{ secrets.AWS_DEPLOY_ROLE_ARN }}
          aws-region: sa-east-1

      - uses: aws-actions/amazon-ecr-login@v2

      - name: Build and push
        run: |
          IMAGE="${{ secrets.ECR_REGISTRY }}/prumo-web"
          docker build -t "$IMAGE:${{ github.sha }}" -t "$IMAGE:latest" .
          docker push "$IMAGE:${{ github.sha }}"
          docker push "$IMAGE:latest"

      - name: Dispatch deploy
        uses: peter-evans/repository-dispatch@v3
        with:
          token: ${{ secrets.DEVOPS_DISPATCH_TOKEN }}
          repository: nickolascheidt/SaaSBasePlatform-DevOps
          event-type: deploy
          client-payload: '{"app":"web","image_tag":"${{ github.sha }}"}'
```

- [ ] **Passo 4: `deploy.yml` no repo devops passa a mandar um comando por SSM**

O job que hoje atualiza o Container App vira:

```yaml
      - uses: aws-actions/configure-aws-credentials@v4
        with:
          role-to-assume: ${{ secrets.AWS_DEPLOY_ROLE_ARN }}
          aws-region: sa-east-1

      - name: Ship the deploy files and restart the stack
        run: |
          INSTANCE_ID="$(aws ec2 describe-instances \
            --filters 'Name=tag:Name,Values=prumo-dev' 'Name=instance-state-name,Values=running' \
            --query 'Reservations[0].Instances[0].InstanceId' --output text)"

          # Os arquivos de deploy viajam como base64 dentro do próprio comando:
          # são pequenos, e assim não é preciso dar credencial de git à instância.
          PAYLOAD="$(tar czf - -C deploy . | base64 -w0)"

          CMD_ID="$(aws ssm send-command \
            --instance-ids "$INSTANCE_ID" \
            --document-name AWS-RunShellScript \
            --parameters commands="[
              \"set -euo pipefail\",
              \"echo $PAYLOAD | base64 -d | tar xzf - -C /opt/prumo\",
              \"cd /opt/prumo\",
              \"sed -i 's/^${{ steps.r.outputs.app == 'api' && 'API' || 'WEB' }}_TAG=.*/${{ steps.r.outputs.app == 'api' && 'API' || 'WEB' }}_TAG=${{ steps.r.outputs.tag }}/' .env\",
              \"aws ecr get-login-password --region sa-east-1 | docker login --username AWS --password-stdin ${{ secrets.ECR_REGISTRY }}\",
              \"docker compose pull\",
              \"docker compose up -d\",
              \"docker image prune -f\"
            ]" \
            --query Command.CommandId --output text)"

          aws ssm wait command-executed --command-id "$CMD_ID" --instance-id "$INSTANCE_ID"
          aws ssm get-command-invocation --command-id "$CMD_ID" --instance-id "$INSTANCE_ID" \
            --query StandardOutputContent --output text
```

- [ ] **Passo 5: Verificar de ponta a ponta**

Faça um commit qualquer no repo app e empurre.

Esperado, nesta ordem: a workflow do app constrói e empurra; o `repository_dispatch` acorda o devops; o `deploy.yml` manda o comando; a saída do `get-command-invocation` mostra o `docker compose up -d`. Depois:

```bash
aws ecr describe-images --repository-name prumo-api \
  --query 'sort_by(imageDetails,&imagePushedAt)[-1].imageTags'
```

Esperado: as tags `latest` e o SHA do commit.

- [ ] **Passo 6: Commitar**

Em cada repo:

```bash
git add .github/workflows/
git commit -m "ci: build to ECR and deploy through SSM instead of Azure"
```

---

## Task 11: Verificação de ponta a ponta

**Repo:** nenhum. É a prova de que a fatia está pronta.

São os sete critérios da spec, em ordem. Marque cada um.

- [ ] **1. O ambiente aplica do zero**

```bash
cd terraform/aws/dev
terraform destroy
terraform apply
```

Esperado: `Apply complete`. Depois dele, dispare um deploy à mão em cada repo de app (`workflow_dispatch`) para que as imagens cheguem à máquina nova.

- [ ] **2. HTTPS com certificado válido**

```bash
curl -sSI https://<DOMINIO> | head -1
echo | openssl s_client -connect <DOMINIO>:443 2>/dev/null | grep -E "issuer|subject"
```

Esperado: `HTTP/2 200`, e o emissor sendo o Let's Encrypt. Se o certificado não sair, veja `docker compose logs caddy` — quase sempre é o DNS ainda não propagado.

- [ ] **3. A tela de login carrega**

Abrir `https://<DOMINIO>` no navegador. Esperado: a tela de login do Angular, sem erro no console.

- [ ] **4. O login funciona, do navegador e do celular**

Entrar com `admin@SBP.com` e a senha que está em `/prumo/dev/seed-admin-password`. Esperado: o dashboard. Repetir do celular, na rede móvel — foi o critério que a Azure teve que passar em 2026-06, e ele pega problema de CORS e de host que o desktop na mesma rede esconde.

- [ ] **5. O sink do Serilog não está reclamando**

```bash
aws ssm start-session --target <INSTANCE_ID>
sudo docker compose -f /opt/prumo/docker-compose.yml logs api | grep -i selflog
```

Esperado: **nada**. Falha de sink é silenciosa neste projeto e já custou meses; o `SelfLog` está ligado justamente para denunciar.

- [ ] **6. A notificação chega no SQS real**

```bash
curl -sS -o /dev/null -w '%{http_code}\n' -X POST https://<DOMINIO>/api/auth/forgot-password \
  -H 'Content-Type: application/json' \
  -d '{"email":"admin@SBP.com"}'
```

Esperado: **202**, não 200 — o endpoint só publica na fila. Depois:

```bash
aws sqs receive-message --queue-url "$(terraform output -raw queue_url)" \
  --message-attribute-names All --wait-time-seconds 10
```

Esperado: uma mensagem com o atributo `Type` valendo `email.password-reset`. **Ninguém a consome** — o worker é a spec seguinte, e a mensagem voltar para a fila é o comportamento correto aqui.

- [ ] **7. O backup aconteceu de verdade**

Este item só pode ser marcado **no dia seguinte** ao primeiro boot: a policy roda 06:00 UTC.

```bash
aws ec2 describe-snapshots --owner-ids self \
  --query 'Snapshots[].{Id:SnapshotId,Vol:VolumeId,When:StartTime,State:State}' \
  --output table
```

Esperado: pelo menos um snapshot `completed` do volume da instância. Policy `ENABLED` prova que a configuração existe; só isto prova que ela funciona — e é a diferença entre ter backup e achar que tem.

- [ ] **8. Destruir, se for o caso — e o que sobrevive**

> **Atenção, mudou em 2026-09-08:** enquanto este ambiente for o piloto, ele fica **ligado**. `terraform destroy` deixou de ser rotina e virou o botão de desistir. Rode este item só se for realmente desmontar tudo, e só depois de conferir o item 7.

```bash
terraform destroy
```

Esperado: tudo removido, **incluindo os repositórios do ECR e as imagens dentro deles** — é para isso que serve o `force_delete`, e o próximo `apply` os recria vazios (o primeiro deploy depois disso precisa reconstruir as imagens).

Sobrevive de propósito só o que está fora da árvore: o bucket de estado criado pela CLI na Task 1, a hosted zone do Route 53 se você tiver criado uma, e **os snapshots já tirados** — o DLM não os apaga junto com a policy, e é bom que não apague: é o que permite ressuscitar o piloto depois de um destroy errado. Eles custam centavos; apague à mão quando tiver certeza. Confira no Billing no dia seguinte que o custo corrente voltou para perto de zero.

---

## Task 12: Registrar a execução

**Repo:** app (é onde os planos vivem) e devops

- [ ] **Passo 1: Preencher o "Registro de execução" no fim deste plano**

O que a execução revelou, e onde este plano errou. É a convenção do repo, e esses registros costumam valer mais que o plano em si — especialmente aqui, porque o HCL foi escrito sem uma conta AWS para validar contra e vai ter errado em algum lugar.

- [ ] **Passo 2: Atualizar o `NEXT_SESSION.md` do repo devops**

Ele hoje descreve a Azure como se ela fosse o presente. Passa a descrever a AWS, com o runbook de ligar e desligar.

- [ ] **Passo 3: Atualizar "Onde o projeto está" em `docs/MVP-BACKLOG.md`**

A linha "O pass único de Azure" deixa de existir. No lugar entram as duas specs seguintes: SES + worker, e o endurecimento do item 11.

- [ ] **Passo 4: Commitar**

```bash
git add docs/superpowers/plans/2026-09-07-aws-dev-environment.md docs/MVP-BACKLOG.md
git commit -m "docs: record what executing the AWS environment plan taught"
```

---

## Riscos conhecidos deste plano

- **`deploy/db/roles.sql` é cópia.** Se `db/roles.sql` mudar no repo da aplicação e a cópia não, um volume novo nasce com roles errados e a API sobe sem permissão. Não há nada impedindo a deriva nesta fatia; a checagem certa é um passo de CI comparando os dois, e ele não está aqui.
- **O HCL foi escrito sem conta AWS para validar.** Espere corrigir nomes de atributo no primeiro `plan` de cada task.
- **`t3.small` são 2 GB para quatro containers.** Se o Postgres e a API brigarem por memória, `t3.medium` é uma linha.
- **O limite de emissão do Let's Encrypt** é por domínio e por semana. Destruir e recriar o ambiente muitas vezes esbarra nele; o volume `caddy-data` preserva o certificado se não for destruído junto.
- **A ordem das tasks 4 e 5 é circular no Terraform**, não na leitura: a policy de deploy referencia a instância. Elas se aplicam juntas, e o plano diz isso — mas quem executar fora de ordem vai ver um erro de referência não resolvida.
- **O backup depende de uma tag.** A policy do DLM encontra o volume por `Name = local.name` na instância. Renomear a tag desliga o backup **em silêncio** — nada falha, os snapshots simplesmente param. É o que o item 7 da Task 11 existe para pegar, e é o que valeria repetir de tempos em tempos.
- **`sslip.io` é uma dependência de terceiro no caminho do TLS.** Se o serviço sair do ar, o nome para de resolver e o Caddy não renova o certificado. Para destravar o primeiro `apply` é aceitável; para o piloto em frente a um cliente, não é — o domínio próprio resolve isso e é barato.
- **São Paulo custa ~60% mais que a Virgínia.** Está decidido e registrado na spec, mas o número precisa aparecer no budget: US$ 40/mês, não US$ 20.

---

## Registro de execução

_(A preencher durante a execução — o que a execução revelou e onde o plano errou.)_
