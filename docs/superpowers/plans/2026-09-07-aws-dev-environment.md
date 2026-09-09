# Ambiente dev na AWS — plano de implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

> **Revisto duas vezes em 2026-09-08.** Primeiro quando o Nickolas enquadrou este ambiente como a **versão piloto para testar o mercado**, e não como caixa de teste descartável: região `sa-east-1`, snapshot diário, domínio deixou de bloquear e `terraform destroy` deixou de ser rotina. Depois quando o custo de ~US$ 32/mês se mostrou alto demais para um piloto que ainda não vendeu nada, e o compute passou de **EC2 para Lightsail** — ~US$ 14/mês. As tasks 3, 5, 7 e 10 foram reescritas; as demais sobreviveram quase intactas.

**Goal:** Pôr o Prumo no ar na AWS em São Paulo, numa única instância Lightsail rodando `docker compose`, com TLS e backup diário, ao ponto de fazer login de um navegador — por ~US$ 14/mês.

**Architecture:** Uma instância Lightsail de 2 GB em `sa-east-1` roda quatro containers — Caddy na borda, o nginx do Angular, a API e o Postgres. O snapshot diário é um add-on da própria instância. O Terraform novo vive em `terraform/aws/dev/` no repo de DevOps, ao lado da árvore `azurerm`, que não é tocada. As imagens são construídas pelo GitHub Actions por OIDC e empurradas para o ECR; o deploy é um script que você roda por SSH. A máquina carrega uma chave IAM limitada a enfileirar no SQS e a puxar imagem do ECR.

**Tech Stack:** Terraform >= 1.10 (backend S3 com locking nativo), AWS provider ~> 6.0, Lightsail com blueprint Amazon Linux 2023, Docker + compose v2, Caddy 2, Postgres 17, .NET 10, Angular.

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

> **Estado verificado no console em 2026-09-08** (conta `7673-9793-9785`, aberta em junho de 2025). O que já está pronto e o que falta:
>
> | | Estado |
> |---|---|
> | Conta existe, região já em São Paulo | ✅ |
> | MFA no usuário raiz | ✅ |
> | Raiz sem chaves de acesso | ✅ |
> | Nada ligado (custo US$ 0,00 no mês e no anterior) | ✅ |
> | Usuário IAM `nickolas` com `AdministratorAccess` pelo grupo `admin` | ✅ |
> | **Acesso ao console desabilitado nesse usuário** — por isso o dia a dia acontece na raiz | ❌ |
> | **MFA nesse usuário** | ❌ |
> | **Chave de acesso de 448 dias, sem uso há 446** | ⚠️ |
> | **Budget alarm** | ❌ |
> | **Créditos: US$ 0,00** — a conta é anterior ao modelo de julho/2025 e o free tier de 12 meses expirou | ⚠️ |

Sobra fazer, nesta ordem:

1. **Dar senha de console ao usuário `nickolas`** (IAM → Usuários → Credenciais de segurança → Habilitar acesso ao console) e **ativar MFA nele**. Hoje ele só tem chave programática, e é por isso que o console está sendo usado pela raiz — não por escolha, por falta de alternativa.
2. **Passar a entrar por `https://767397939785.signin.aws.amazon.com/console`** com esse usuário, e deixar a raiz para o que só ela faz (fechar a conta, mudar plano de suporte, alterar forma de pagamento).
3. **Rotacionar a chave de acesso.** Ela tem 448 dias e não é usada há 446 — ou seja, ninguém sabe onde ela está. Criar a chave 2, pôr no `aws configure`, confirmar com `aws sts get-caller-identity` e **desativar e apagar a chave 1**.
4. **Criar o budget alarm** em Billing: US$ 40/mês, alerta por e-mail em 50% e 100%. (Eram US$ 20 quando o alvo era `us-east-1`; São Paulo custa ~US$ 32/mês ligado 24/7, e um limite abaixo do custo real só ensina a ignorar o alerta.) **Sem crédito nenhum na conta, este é o único aviso que existe entre um erro e uma fatura de verdade.**

As quatro políticas extras no grupo `admin` (`AmazonEC2FullAccess`, `AmazonS3FullAccess`, `AmazonVPCFullAccess`, `IAMFullAccess`) são redundantes com `AdministratorAccess` e podem sair quando der vontade. Não bloqueiam nada.

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

- **Não tem domínio, ou não quer gastar agora:** não faça nada aqui. O ambiente sobe com `sslip.io`, que resolve `<ip-com-hifens>.sslip.io` para o IP sem cadastro nenhum. O Caddy tira certificado válido do Let's Encrypt normalmente. `<DOMINIO>` no resto do plano vira esse nome, e você só o conhece depois da Task 5 (é o IP estático da instância) — a Task 8 diz onde preenchê-lo.
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

variable "bundle_id" {
  description = "Plano do Lightsail. small_3_0 são 2 GB, 2 vCPU, 60 GB de SSD e IP estático por US$ 12/mês fixos. Se apertar, medium_3_0 (4 GB, US$ 24)."
  type        = string
  default     = "small_3_0"
}

variable "blueprint_id" {
  description = "Imagem base do Lightsail. Confirme o id exato com `aws lightsail get-blueprints` antes do primeiro apply — a AWS renomeia blueprints entre releases."
  type        = string
  default     = "amazon_linux_2023"
}

variable "availability_zone" {
  description = "AZ do Lightsail. Ao contrário da EC2, ele exige a AZ explícita na criação."
  type        = string
  default     = "sa-east-1a"
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

locals {
  name         = "${var.project}-dev"
  account_id   = data.aws_caller_identity.current.account_id
  ecr_registry = "${data.aws_caller_identity.current.account_id}.dkr.ecr.${var.aws_region}.amazonaws.com"
}
```

Repare no que **não** está aqui e estava na versão EC2 deste plano: VPC default, lista de subnets e a AMI do Amazon Linux por parâmetro público do SSM. O Lightsail não expõe nenhuma dessas coisas — ele tem rede própria, e a imagem base é escolhida por `blueprint_id`. É o primeiro sinal concreto de que a árvore ficou menor.

- [ ] **Passo 5: `outputs.tf`**

```terraform
output "ecr_registry" {
  description = "Host do ECR, usado no docker login e nos nomes de imagem."
  value       = local.ecr_registry
}
```

- [ ] **Passo 6: `terraform.tfvars`**

```terraform
aws_region = "sa-east-1"

# Sem domínio próprio, deixe como está e volte aqui depois da Task 5: o nome vira
# o IP estático com hífens, ex. "54-207-1-2.sslip.io" (Task 8, Passo 0).
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

## Task 3: Os segredos, num `.env` na própria máquina

**Repo:** devops

**Files:** nenhum. É trabalho de máquina, feito uma vez.

> **Esta task encolheu na revisão de 2026-09-08.** Na versão EC2 ela criava cinco
> parâmetros SecureString no SSM Parameter Store, que a instância lia no boot pelo
> instance profile. O Lightsail não tem instance profile: para ler o SSM, a máquina
> precisaria de uma chave IAM com `ssm:GetParameter`, e uma chave que lê todos os
> segredos do ambiente é exatamente o que não se quer deixar num disco.
>
> Então os segredos ficam onde já iam parar de qualquer jeito: **no `.env` que o
> compose lê, na máquina**. O Parameter Store não acrescentava segurança nenhuma
> aqui — só uma volta a mais e uma permissão perigosa.

- [ ] **Passo 1: Gerar os segredos**

Rode na sua máquina e guarde a saída no seu gerenciador de senhas antes de qualquer outra coisa. Estes valores não ficam guardados em lugar nenhum na AWS.

```bash
echo "JWT_KEY=$(openssl rand -base64 48)"
echo "POSTGRES_PASSWORD=$(openssl rand -base64 24 | tr -d '/+=')"
echo "PRUMO_APP_PASSWORD=$(openssl rand -base64 24 | tr -d '/+=')"
echo "PRUMO_MIGRATOR_PASSWORD=$(openssl rand -base64 24 | tr -d '/+=')"
```

O `tr -d '/+='` existe porque esses caracteres quebram a connection string do Postgres quando entram na senha sem escape.

A senha do admin semeado precisa passar na política do Identity (mínimo 6, com dígito, minúscula e maiúscula — ver `DatabaseConfiguration.cs`), então escolha uma à mão em vez de gerar. Ela é a senha do primeiro login no piloto: trate como senha de verdade, não como `TrocarDepois1`.

- [ ] **Passo 2: Onde eles entram**

O `.env` é escrito na máquina na Task 7, depois que ela existir — não agora. O que esta task entrega é o conjunto de valores, gerado uma vez, guardado por você.

Guarde também **o que cada um é**, porque daqui a três meses ninguém lembra:

| Variável | O que é |
|---|---|
| `JWT_KEY` | assina os tokens; trocar desloga todo mundo |
| `POSTGRES_PASSWORD` | superusuário do Postgres, usado só na criação do volume |
| `PRUMO_APP_PASSWORD` | role `prumo_app`, sem DDL — é com ela que a API serve |
| `PRUMO_MIGRATOR_PASSWORD` | role `prumo_migrator`, que roda a migration no startup |
| `SEED_ADMIN_PASSWORD` | primeiro login no sistema |

- [ ] **Passo 3: Nada para commitar**

Não existe arquivo desta task no repo, e é de propósito. Se você se pegou querendo commitar o `.env`, pare: ele mora na máquina e no seu gerenciador de senhas.

---

## Task 4: OIDC do GitHub e a role de deploy

**Repo:** devops

**Files:**
- Create: `terraform/aws/dev/oidc.tf`

Substitui as federated credentials do Entra. Nenhuma chave de acesso da AWS vai para os secrets do GitHub — o CI se autentica por OIDC, que continua valendo igual no desenho com Lightsail. **A ordem circular com a Task 5 acabou:** sem `ssm:SendCommand`, esta role não referencia mais a instância, e a Task 4 aplica sozinha.

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

  # E nada mais. Na versão EC2 deste plano havia aqui permissão de
  # `ssm:SendCommand` para disparar o deploy na instância; o Lightsail não é
  # alcançável por SSM Run Command, então o deploy vira SSH (Task 10) e esta role
  # passa a fazer só uma coisa: empurrar imagem.
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

**Nota, mudou em 2026-09-08:** na versão EC2 esta task referenciava `aws_instance.app` (para o `ssm:SendCommand`) e precisava ser aplicada junto com a Task 5. Sem isso, ela é independente: pode aplicar sozinha.

- [ ] **Passo 3: Commitar (sem aplicar ainda)**

```bash
git add terraform/aws/dev/oidc.tf terraform/aws/dev/outputs.tf
git commit -m "feat(aws): trust GitHub Actions through OIDC for deploys"
```

---

## Task 5: A instância Lightsail, o IP estático e a chave da máquina

**Repo:** devops

**Files:**
- Create: `terraform/aws/dev/instance.tf`
- Create: `terraform/aws/dev/runtime-user.tf`

> **Esta task foi reescrita em 2026-09-08**, quando o compute passou de EC2 para
> Lightsail. Sumiram: security group, VPC default, IP elástico, instance profile,
> role da instância e a policy de Data Lifecycle Manager. Entraram: uma instância,
> um IP estático, uma regra de portas, um add-on de snapshot e um usuário IAM.
> São menos recursos e menos conceitos — e é por isso que o console fica legível.

- [ ] **Passo 1: A chave SSH, gerada na sua máquina**

O Lightsail não tem SSM Session Manager, então voltou a existir uma chave SSH. Ela é gerada **local** e só a metade pública sobe: a privada nunca passa pelo Terraform, e portanto nunca entra no state file.

```bash
ssh-keygen -t ed25519 -f ~/.ssh/prumo-dev -C "prumo-dev" -N ""
cat ~/.ssh/prumo-dev.pub
```

Guarde `~/.ssh/prumo-dev` no gerenciador de senhas junto com os segredos da Task 3. Perder essa chave é perder o acesso à máquina — no Lightsail não há Session Manager para socorrer.

- [ ] **Passo 2: `instance.tf`**

```terraform
# A metade pública da chave do Passo 1. `file()` lê no apply, da sua máquina.
resource "aws_lightsail_key_pair" "app" {
  name       = local.name
  public_key = file("~/.ssh/prumo-dev.pub")
}

resource "aws_lightsail_instance" "app" {
  name              = local.name
  availability_zone = var.availability_zone
  blueprint_id      = var.blueprint_id
  bundle_id         = var.bundle_id
  key_pair_name     = aws_lightsail_key_pair.app.name

  user_data = file("${path.module}/user-data.sh")

  # O backup inteiro, no lugar da role + policy de DLM da versão EC2. O Lightsail
  # guarda os 7 snapshots automáticos mais recentes e apaga o resto sozinho.
  # O horário é UTC e precisa ser hora cheia: 06:00 UTC = 03:00 em Brasília.
  add_on {
    type                 = "AutoSnapshot"
    snapshot_time_of_day = "06:00"
    status               = "Enabled"
  }

  tags = { Name = local.name }
}

# IP fixo: sem ele o endereço muda a cada recriação, e com ele mudam o nome
# sslip.io e o certificado do Caddy.
resource "aws_lightsail_static_ip" "app" {
  name = local.name
}

resource "aws_lightsail_static_ip_attachment" "app" {
  static_ip_name = aws_lightsail_static_ip.app.name
  instance_name  = aws_lightsail_instance.app.name
}

# O Lightsail abre 22 e 80 por padrão. Isto substitui a regra inteira: 80 e 443
# para todo mundo, 22 só do seu IP.
resource "aws_lightsail_instance_public_ports" "app" {
  instance_name = aws_lightsail_instance.app.name

  port_info {
    protocol  = "tcp"
    from_port = 80
    to_port   = 80
    cidrs     = ["0.0.0.0/0"]
  }

  port_info {
    protocol  = "tcp"
    from_port = 443
    to_port   = 443
    cidrs     = ["0.0.0.0/0"]
  }

  port_info {
    protocol  = "tcp"
    from_port = 22
    to_port   = 22
    cidrs     = [var.ssh_allowed_cidr]
  }
}
```

Acrescente a variável que a última regra usa, em `variables.tf`:

```terraform
variable "ssh_allowed_cidr" {
  description = "De onde o SSH é aceito, ex.: 189.10.20.30/32. Descubra o seu com `curl -s ifconfig.me`. Deixar 0.0.0.0/0 expõe a porta 22 ao mundo — é a diferença de segurança mais visível entre este desenho e o de EC2, que não tinha porta 22 nenhuma."
  type        = string
}
```

Se o seu IP residencial muda, ponha o de hoje e ajuste quando quebrar. É uma linha de `terraform apply`, e é bem melhor que deixar aberto.

- [ ] **Passo 3: `runtime-user.tf` — a chave que a máquina carrega**

Este é **o preço de ter escolhido Lightsail**, e vale entender o tamanho exato dele. Sem instance profile, a máquina precisa de uma credencial gravada em disco para falar com a AWS. A defesa é dar a essa credencial o menor poder possível.

```terraform
resource "aws_iam_user" "box" {
  name = "${local.name}-box"
}

data "aws_iam_policy_document" "box" {
  # Enfileirar notificação. É a única escrita que a máquina faz na AWS.
  statement {
    effect    = "Allow"
    actions   = ["sqs:SendMessage", "sqs:GetQueueUrl", "sqs:GetQueueAttributes"]
    resources = [aws_sqs_queue.notifications.arn]
  }

  # Puxar as imagens. Só leitura, e só destes dois repositórios.
  statement {
    effect = "Allow"
    actions = [
      "ecr:BatchGetImage",
      "ecr:GetDownloadUrlForLayer",
      "ecr:BatchCheckLayerAvailability",
    ]
    resources = [aws_ecr_repository.api.arn, aws_ecr_repository.web.arn]
  }

  # O token de login do ECR não aceita recurso específico.
  statement {
    effect    = "Allow"
    actions   = ["ecr:GetAuthorizationToken"]
    resources = ["*"]
  }
}

resource "aws_iam_user_policy" "box" {
  name   = "runtime"
  user   = aws_iam_user.box.name
  policy = data.aws_iam_policy_document.box.json
}
```

**Não existe `aws_iam_access_key` aqui, e é de propósito:** o Terraform guardaria a chave secreta em texto claro no state file. Ela é criada uma vez, à mão:

```bash
aws iam create-access-key --user-name prumo-dev-box
```

Guarde a saída no gerenciador de senhas. Ela entra no `.env` da máquina na Task 7.

**O que um vazamento dessa chave dá ao atacante, na íntegra:** enfileirar mensagens na sua fila de notificação, e baixar as suas imagens de container. Não dá para ler segredo, não dá para criar recurso, não dá para mexer na instância. É um estrago pequeno e limitado — mas não é zero, e na versão EC2 era zero. Se um dia isso incomodar, é o motivo certo para voltar para EC2.

- [ ] **Passo 4: Saídas em `outputs.tf`**

```terraform
output "public_ip" {
  description = "IP estático da instância. Vira o nome sslip.io e o alvo do registro A."
  value       = aws_lightsail_static_ip.app.ip_address
}

output "ssh" {
  description = "Como entrar na máquina."
  value       = "ssh -i ~/.ssh/prumo-dev ec2-user@${aws_lightsail_static_ip.app.ip_address}"
}
```

O usuário do SSH depende do blueprint: `ec2-user` no Amazon Linux 2023, `ubuntu` nos blueprints Ubuntu. Confira no Passo 6 antes de assumir.

- [ ] **Passo 5: Conferir os ids e aplicar**

Blueprint e bundle são os dois lugares onde este plano tem mais chance de estar desatualizado. Confirme antes:

```bash
aws lightsail get-blueprints --query 'blueprints[?type==`os`].[blueprintId,name]' --output table
aws lightsail get-bundles --query 'bundles[].[bundleId,ramSizeInGb,price]' --output table
```

Esperado: `amazon_linux_2023` na primeira lista, e `small_3_0` com 2 GB e preço 12 na segunda. Se os nomes divergirem, ajuste `variables.tf` — o resto do plano não muda.

```bash
terraform apply
```

Esperado: a chave, a instância, o IP estático, a ligação entre eles, as regras de porta e o usuário IAM. O `user-data.sh` ainda não existe — a Task 7 o cria; para este primeiro apply, comente a linha `user_data` e descomente lá.

- [ ] **Passo 6: Entrar na máquina**

```bash
terraform output -raw public_ip
ssh -i ~/.ssh/prumo-dev ec2-user@$(terraform output -raw public_ip)
```

Esperado: um shell. Se o SSH recusar, os suspeitos são três, nesta ordem: `ssh_allowed_cidr` diferente do seu IP de agora (`curl -s ifconfig.me`), usuário errado para o blueprint, ou a instância ainda subindo.

- [ ] **Passo 7: Confirmar que o snapshot automático está ligado**

```bash
aws lightsail get-instance --instance-name prumo-dev \
  --query 'instance.addOns[].{Name:name,Status:status,At:snapshotTimeOfDay}' --output table
```

Esperado: `AutoSnapshot`, `Enabled`, `06:00`. Isto prova que está configurado; **que ele realmente tira snapshot se verifica no dia seguinte**, na Task 11.

- [ ] **Passo 8: Commitar**

```bash
git add terraform/aws/dev/instance.tf terraform/aws/dev/runtime-user.tf terraform/aws/dev/variables.tf terraform/aws/dev/outputs.tf
git commit -m "feat(aws): create the Lightsail box, its static IP and its scoped runtime user"
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
      # A credencial do SDK. No Lightsail não há instance profile: estas duas vem
      # do .env, e sao a chave escopada do usuario prumo-dev-box (Task 5).
      AWS_ACCESS_KEY_ID: ${AWS_ACCESS_KEY_ID}
      AWS_SECRET_ACCESS_KEY: ${AWS_SECRET_ACCESS_KEY}
      # Sqs__ServiceUrl fica AUSENTE de propósito. Preenchida, ela apontaria o SDK
      # para o emulador — e a guarda de NotificationConfiguration recusa o startup.
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

## Task 7: O user-data que monta a máquina, e o `.env` posto à mão

**Repo:** devops

**Files:**
- Create: `terraform/aws/dev/user-data.sh`
- Modify: `terraform/aws/dev/main.tf`

> **Reescrita em 2026-09-08.** Na versão EC2 o script buscava os cinco segredos no
> SSM Parameter Store usando o instance profile, e escrevia o `.env` sozinho. Sem
> instance profile, isso exigiria uma chave capaz de ler todos os segredos — o
> oposto do que a Task 5 fez. Então o script instala a máquina, e **o `.env` é
> escrito por você, uma vez, por SSH**. É um passo manual a mais e uma permissão
> perigosa a menos.

- [ ] **Passo 1: `user-data.sh`**

```bash
#!/bin/bash
set -euxo pipefail

# --- Docker ---
dnf install -y docker git
systemctl enable --now docker
usermod -aG docker ec2-user

# O AL2023 não empacota o plugin compose v2; ele vem do release do Docker.
mkdir -p /usr/local/lib/docker/cli-plugins
curl -fsSL \
  "https://github.com/docker/compose/releases/download/v2.29.7/docker-compose-linux-x86_64" \
  -o /usr/local/lib/docker/cli-plugins/docker-compose
chmod +x /usr/local/lib/docker/cli-plugins/docker-compose

# --- AWS CLI, para o login no ECR ---
dnf install -y awscli-2 || dnf install -y aws-cli

# --- diretório de deploy ---
# Os arquivos (docker-compose.yml, Caddyfile, db/roles.sql) chegam pelo deploy da
# Task 10. No primeiro boot eles ainda não estão, e isso não é erro.
mkdir -p /opt/prumo/db
chown -R ec2-user:ec2-user /opt/prumo
```

Repare no que sumiu: nenhum `templatefile`, nenhuma variável interpolada, nenhum segredo. O script virou instalação de máquina pura — é o mesmo em qualquer ambiente, e por isso não precisa mais ser reaplicado quando um valor muda.

- [ ] **Passo 2: Ligar o user-data em `instance.tf`**

Descomente a linha no recurso da Task 5:

```terraform
user_data = file("${path.module}/user-data.sh")
```

`file()` em vez de `templatefile()`, porque não há mais nada para interpolar.

**Aviso do Lightsail, diferente da EC2:** ele não tem o equivalente de `user_data_replace_on_change`. Mudar o script **não** recria a instância — o Terraform aceita a mudança e não faz nada com ela. Para que um script novo valha, é preciso destruir e recriar a instância (`terraform taint aws_lightsail_instance.app`), ou aplicar a mudança à mão por SSH. Como este script só instala Docker, isso quase nunca vai importar; mas quando importar, vai importar em silêncio.

- [ ] **Passo 3: Aplicar**

```bash
terraform apply
```

- [ ] **Passo 4: Escrever o `.env` na máquina**

Os valores são os da Task 3, mais a chave de acesso criada na Task 5 e o IP da máquina. Entre por SSH e escreva:

```bash
ssh -i ~/.ssh/prumo-dev ec2-user@$(terraform output -raw public_ip)

cat > /opt/prumo/.env <<'EOF'
DOMAIN=54-207-1-2.sslip.io
ECR_REGISTRY=767397939785.dkr.ecr.sa-east-1.amazonaws.com
AWS_REGION=sa-east-1
AWS_ACCESS_KEY_ID=AKIA...
AWS_SECRET_ACCESS_KEY=...
SQS_QUEUE_URL=https://sqs.sa-east-1.amazonaws.com/767397939785/prumo-dev-notifications
API_TAG=latest
WEB_TAG=latest
JWT_KEY=...
SEED_ADMIN_PASSWORD=...
POSTGRES_PASSWORD=...
PRUMO_APP_PASSWORD=...
PRUMO_MIGRATOR_PASSWORD=...
EOF
chmod 600 /opt/prumo/.env
```

O `<<'EOF'` com aspas é obrigatório: sem elas o shell expande `$` dentro dos segredos e você grava uma senha diferente da que gerou. Isso já custou tempo de gente melhor.

`DOMAIN` sai da Task 8 e `SQS_QUEUE_URL` do `terraform output` da Task 2. Se ainda não souber o domínio, ponha o nome `sslip.io` do IP — ele já é o valor final no caminho sem domínio próprio.

- [ ] **Passo 5: Login no ECR, na máquina**

```bash
set -a && . /opt/prumo/.env && set +a
aws ecr get-login-password --region "$AWS_REGION" \
  | docker login --username AWS --password-stdin "$ECR_REGISTRY"
```

Esperado: `Login Succeeded`. Se falhar com `AccessDenied`, a policy do usuário `prumo-dev-box` (Task 5, Passo 3) é o lugar para olhar.

Esse login expira em 12 horas. O deploy da Task 10 refaz o login toda vez, então não vira problema recorrente — mas explica por que um `docker compose pull` manual pode falhar dias depois.

- [ ] **Passo 6: Verificar o boot**

```bash
sudo tail -40 /var/log/cloud-init-output.log
docker compose version
ls -l /opt/prumo/.env
```

Esperado: o log sem erro, `Docker Compose version v2.29.7`, e o `.env` com permissão `600` e dono `ec2-user`.

- [ ] **Passo 7: Commitar**

```bash
git add terraform/aws/dev/user-data.sh terraform/aws/dev/instance.tf
git commit -m "feat(aws): bootstrap docker from user-data"
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

- [x] **Passo 1: Escrever o teste que falha**

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

- [x] **Passo 2: Rodar e ver falhar**

```bash
dotnet test --filter "FullyQualifiedName~ProductionConfigTests"
```

Esperado: `O_overlay_de_producao_nao_carrega_placeholder_de_azure` **falha** — o arquivo contém `CONFIGURE_VIA_AZURE_APP_SETTINGS_OR_KEY_VAULT` e `yourdomain.com`. O segundo teste passa.

- [x] **Passo 3: Corrigir o overlay**

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

- [x] **Passo 4: Trocar a região default de `us-east-1` para `sa-east-1`**

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

- [x] **Passo 5: Rodar e ver passar**

```bash
dotnet test
```

Esperado: **138 aprovados**, 0 falhas (136 de hoje mais os 2 novos).

- [x] **Passo 6: Commitar**

```bash
git add Prumo.Api/appsettings.Production.json Prumo.Api/appsettings.json \
        Prumo.Api/Configuration/NotificationConfiguration.cs \
        Prumo.Notifications/appsettings.json Prumo.Notifications/Program.cs \
        Prumo.Tests/Architecture/ProductionConfigTests.cs
git commit -m "fix(config): stop naming Azure in the production overlay and default to sa-east-1"
```

---

## Task 10: Build para o ECR no CI, deploy por um comando seu

**Repos:** app, angular, devops

**Files:**
- Modify: `.github/workflows/*.yml` no repo **app** (o que hoje empurra para o ACR)
- Modify: `.github/workflows/*.yml` no repo **angular**
- Create: `deploy/deploy.sh` no repo **devops**
- Delete: `.github/workflows/deploy.yml` no repo **devops** (ou deixe parado; ver o Passo 4)

> **Reescrita em 2026-09-08.** Na versão EC2 o deploy era um SSM Run Command
> disparado pelo GitHub Actions — sem porta 22 aberta e sem chave nenhuma. O
> Lightsail não é alcançável por SSM Run Command, então sobraram dois caminhos:
> abrir a porta 22 para o mundo e guardar uma chave SSH nos secrets do GitHub, ou
> **manter a 22 fechada e disparar o deploy da sua máquina**.
>
> Escolhi o segundo. Para um piloto com uma pessoa, `./deploy.sh` é 30 segundos, e
> a alternativa custa a porta 22 exposta mais uma chave privada num secret. O dia
> em que houver mais de uma pessoa fazendo deploy, o Passo 4 diz como inverter.

- [ ] **Passo 1: Pôr os secrets nos repos app e angular**

```bash
cd terraform/aws/dev
terraform output -raw github_deploy_role_arn
terraform output -raw ecr_registry
```

Nos repos **app** e **angular**, criar dois secrets: `AWS_DEPLOY_ROLE_ARN` e `ECR_REGISTRY` com esses valores. O repo devops não precisa de secret nenhum — ele deixou de fazer deploy.

Os secrets antigos do Azure (`ACR_LOGIN_SERVER`, credenciais do Entra) ficam onde estão até a limpeza final; não custam nada parados. O `DEVOPS_DISPATCH_TOKEN` também deixa de ser usado — nada mais acorda o repo devops.

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
```

O `repository_dispatch` que existia no fim deste job **sai**: não há mais deploy automático para acordar.

- [ ] **Passo 3: O mesmo no repo angular**

Idêntico, trocando `prumo-api` por `prumo-web`. O `Dockerfile` do Angular não muda — a imagem nginx com o `proxy_pass` literal continua exatamente como está, e essa é a decisão 6 da spec.

- [ ] **Passo 4: `deploy/deploy.sh` no repo devops**

```bash
#!/usr/bin/env bash
# Sobe a stack na máquina do piloto. Roda da sua máquina, não do CI.
# Uso: ./deploy.sh          (usa as imagens :latest)
#      ./deploy.sh <sha>    (fixa uma versão específica das duas imagens)
set -euo pipefail

HOST="$(cd "$(dirname "$0")/../terraform/aws/dev" && terraform output -raw public_ip)"
SSH="ssh -i $HOME/.ssh/prumo-dev ec2-user@$HOST"
TAG="${1:-latest}"

# Os arquivos de deploy viajam por SSH; a máquina não precisa de credencial de git.
tar czf - -C "$(dirname "$0")" docker-compose.yml Caddyfile db \
  | $SSH "tar xzf - -C /opt/prumo"

$SSH bash -s <<EOF
set -euo pipefail
cd /opt/prumo
sed -i "s/^API_TAG=.*/API_TAG=$TAG/" .env
sed -i "s/^WEB_TAG=.*/WEB_TAG=$TAG/" .env
set -a && . ./.env && set +a
aws ecr get-login-password --region "\$AWS_REGION" \
  | docker login --username AWS --password-stdin "\$ECR_REGISTRY"
docker compose pull
docker compose up -d
docker image prune -f
docker compose ps
EOF
```

```bash
chmod +x deploy/deploy.sh
```

O `<<EOF` sem aspas é proposital aqui, ao contrário do `.env` da Task 7: `$TAG` precisa ser expandido pela **sua** máquina, e por isso `\$AWS_REGION` e `\$ECR_REGISTRY` estão escapados para sobrarem para a máquina remota. Trocar isso quebra de um jeito silencioso.

O `deploy.yml` do repo devops fica órfão. Apagar ou deixar parado dá no mesmo — só não deixe ele com trigger automático apontando para a Azure.

**Para inverter no futuro**, quando deploy manual cansar: abrir a 22 para `0.0.0.0/0` em `instance.tf`, gerar um par de chaves só para deploy, pôr a privada num secret `DEPLOY_SSH_KEY` do repo devops e transformar este script no corpo de um job. O script é o mesmo; muda quem o executa.

- [ ] **Passo 5: Verificar de ponta a ponta**

Faça um commit qualquer no repo app e empurre. Esperado: a workflow constrói e empurra, e nada mais acontece — o deploy é seu.

```bash
aws ecr describe-images --repository-name prumo-api \
  --query 'sort_by(imageDetails,&imagePushedAt)[-1].imageTags'
```

Esperado: as tags `latest` e o SHA do commit.

```bash
cd deploy && ./deploy.sh
```

Esperado: `docker compose ps` no fim listando `caddy`, `web`, `api` e `postgres` como `running`.

- [ ] **Passo 6: Commitar**

Em cada repo:

```bash
git add .github/workflows/           # app e angular
git commit -m "ci: build to ECR instead of ACR"

git add deploy/deploy.sh             # devops
git commit -m "feat(deploy): ship the stack to Lightsail over SSH"
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

Esperado: `Apply complete`. Depois dele, duas coisas que a versão EC2 deste plano não precisava: reescrever o `.env` (Task 7, Passo 4 — ele mora no disco, e o disco foi embora junto) e rodar `deploy/deploy.sh` para as imagens chegarem à máquina nova.

- [ ] **2. HTTPS com certificado válido**

```bash
curl -sSI https://<DOMINIO> | head -1
echo | openssl s_client -connect <DOMINIO>:443 2>/dev/null | grep -E "issuer|subject"
```

Esperado: `HTTP/2 200`, e o emissor sendo o Let's Encrypt. Se o certificado não sair, veja `docker compose logs caddy` — quase sempre é o DNS ainda não propagado.

- [ ] **3. A tela de login carrega**

Abrir `https://<DOMINIO>` no navegador. Esperado: a tela de login do Angular, sem erro no console.

- [ ] **4. O login funciona, do navegador e do celular**

Entrar com `admin@SBP.com` e o `SEED_ADMIN_PASSWORD` que você gerou na Task 3 e guardou no gerenciador de senhas. Esperado: o dashboard. Repetir do celular, na rede móvel — foi o critério que a Azure teve que passar em 2026-06, e ele pega problema de CORS e de host que o desktop na mesma rede esconde.

- [ ] **5. O sink do Serilog não está reclamando**

```bash
ssh -i ~/.ssh/prumo-dev ec2-user@$(terraform output -raw public_ip)
cd /opt/prumo && docker compose logs api | grep -i selflog
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

Este item só pode ser marcado **no dia seguinte** ao primeiro boot: o add-on dispara às 06:00 UTC.

```bash
aws lightsail get-auto-snapshots --resource-name prumo-dev \
  --query 'autoSnapshots[].{Date:date,Status:status}' --output table
```

Esperado: pelo menos uma linha com status `Success`. O add-on `Enabled` prova que está configurado; só isto prova que funciona — e é a diferença entre ter backup e achar que tem.

- [ ] **8. Destruir, se for o caso — e o que sobrevive**

> **Atenção, mudou em 2026-09-08:** enquanto este ambiente for o piloto, ele fica **ligado**. `terraform destroy` deixou de ser rotina e virou o botão de desistir. Rode este item só se for realmente desmontar tudo, e só depois de conferir o item 7.

```bash
terraform destroy
```

Esperado: tudo removido, **incluindo os repositórios do ECR e as imagens dentro deles** — é para isso que serve o `force_delete`, e o próximo `apply` os recria vazios (o primeiro deploy depois disso precisa reconstruir as imagens).

Sobrevive de propósito o que está fora da árvore: o bucket de estado criado pela CLI na Task 1, a hosted zone do Route 53 se você tiver criado uma, e **os snapshots automáticos já tirados**.

Guardar os snapshots é proposital — são o que permite ressuscitar o piloto depois de um destroy errado, e custam centavos. Apague à mão quando tiver certeza:

```bash
aws lightsail get-auto-snapshots --resource-name prumo-dev
aws lightsail delete-auto-snapshot --resource-name prumo-dev --date <YYYY-MM-DD>
```

**E uma coisa que o `destroy` não alcança:** a chave de acesso do usuário `prumo-dev-box` foi criada à mão na Task 5, então o Terraform não sabe que ela existe. O usuário some; a chave fica ativa e órfã. Apague junto:

```bash
aws iam list-access-keys --user-name prumo-dev-box
aws iam delete-access-key --user-name prumo-dev-box --access-key-id <AKIA...>
```

Confira no Billing no dia seguinte que o custo corrente voltou para perto de zero.

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
- **O HCL foi escrito sem conta AWS para validar,** e a parte de Lightsail nunca foi aplicada por ninguém neste projeto. Espere corrigir nomes de atributo no primeiro `plan` — começando por `blueprint_id` e `bundle_id`, que a Task 5 manda conferir com a CLI antes de aplicar.
- **A chave IAM na máquina é o preço do Lightsail.** Ela pode enfileirar mensagem e puxar imagem, e nada mais. É estrago pequeno, mas na versão EC2 era zero. Está registrado na spec como a razão certa para voltar para EC2 um dia.
- **A porta 22 existe agora,** restrita ao seu IP. Se o seu IP residencial mudar, o SSH para de funcionar — e como o Lightsail não tem Session Manager, a única saída é um `terraform apply` corrigindo o CIDR. Perder a chave privada é pior: não há socorro, só recriar a máquina a partir do snapshot.
- **Trocar o `user-data.sh` não recria a instância** no Lightsail, ao contrário da EC2 com `user_data_replace_on_change`. O Terraform aceita a mudança e não faz nada com ela, em silêncio. Está dito na Task 7, e vale reler no dia em que algo "deveria estar instalado" e não está.
- **O snapshot é do disco, com o Postgres escrevendo.** É backup a frio: recupera, mas pode passar por recuperação de crash no boot do banco. `pg_dump` para o S3 é o passo seguinte quando houver dado que doa perder, e não está nesta fatia.
- **`sslip.io` é dependência de terceiro no caminho do TLS.** Se sair do ar, o nome para de resolver e o Caddy não renova o certificado. Para destravar o primeiro `apply` é aceitável; para o piloto em frente a um cliente, não é.
- **O budget de US$ 40 continua valendo,** mesmo com o custo esperado caindo para ~US$ 14. Ele existe para pegar o inesperado, não para espelhar o esperado — e sem crédito nenhum na conta, é o único aviso que existe.

---

## Registro de execução

_(A preencher durante a execução — o que a execução revelou e onde o plano errou.)_
