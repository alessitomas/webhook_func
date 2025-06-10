# Webhook Server Funcional com Suave (F#)

Este projeto demonstra **como desenvolver um servidor de webhook** em F#, utilizando o framework [Suave](https://suave.io/). O objetivo é processar webhooks de forma **assíncrona**, validar payloads recebidos e realizar **callbacks HTTP** para confirmação ou cancelamento de transações.

Além disso, um cliente Python com FastAPI é utilizado para simular chamadas de webhook e verificar o comportamento do servidor.

---

## Proposta

Desenvolver um servidor de webhook que:

- Valida payloads recebidos de forma robusta
- Garante **idempotência** (evita processar a mesma transação mais de uma vez)
- Executa chamadas HTTP assíncronas para sistemas externos (confirmar ou cancelar transações)
- É testável, previsível e com baixo acoplamento por meio de **Programação Funcional**

---

## Por que usar Programação Funcional?

- **Sem efeitos colaterais**: facilita testar e depurar  
- **Imutabilidade**: reduz bugs relacionados à concorrência  
- **Funções puras**: isolam claramente a lógica do domínio  
- **Composição**: facilita criar pipelines de transformação e manipulação de dados

---

## Restrições do Problema

O projeto lida com os seguintes desafios:

- Requisições HTTP (recebimento e callbacks)
- Comunicação assíncrona e não bloqueante
- Serialização/Deserialização de payloads JSON
- Tolerância a falhas de rede (e.g. retries)
- Garantia de **idempotência** no tratamento de eventos

---

## Simulador de Webhook (Python)

Um servidor auxiliar escrito com **FastAPI** roda localmente e oferece endpoints `/confirmar` e `/cancelar` para testar callbacks realizados pelo servidor F#.

### Como usar

```bash
# Crie o ambiente
python3 -m venv env
source env/bin/activate  # ou env\Scripts\activate no Windows

# Instale as dependências
pip -r requirements.txt

# Rode o servidor auxiliar
python3 test_webhook.py
```

---

## Testes Incluídos

O script Python realiza automaticamente os seguintes testes:

| # | Situação                              | Esperado           |
|---|---------------------------------------|--------------------|
| 1 | Transação válida                      | ✅ Confirmada       |
| 2 | Transação duplicada                   | ❌ Rejeitada        |
| 3 | Valor inválido (`amount`)             | ❌ Cancelada        |
| 4 | Token inválido                        | ❌ Rejeitada        |
| 5 | Payload vazio                         | ❌ Rejeitado        |
| 6 | Campos ausentes (ex: `timestamp`)     | ❌ Cancelada        |

---

## Estrutura Esperada

- **Endpoint do Webhook** (servidor F#):  
  `POST http://localhost:8080/webhook`

- **Endpoints de Callback simulados (FastAPI)**:
  - `POST http://localhost:5001/confirmar`
  - `POST http://localhost:5001/cancelar`

---

## Exemplo de Header e Payload

    Content-Type: application/json
    X-Webhook-Token: meu-token-secreto

```json
{
  "event": "payment_success",
  "transaction_id": "abc123",
  "amount": "49.90",
  "currency": "BRL",
  "timestamp": "2023-10-01T12:00:00Z"
}  
```


## 🔧 Funções em F#

### `parseJson<'T> (body: string) : Result<'T, string>`
Deserializa uma string JSON em um tipo F# específico.

- ✅ Retorna `Ok valor` se a desserialização for bem-sucedida.
- ❌ Retorna `Error` com mensagem de erro se falhar.

---

### `isValidToken token`
Verifica se o token recebido no header é válido (`"meu-token-secreto"`).

- ✅ Retorna `true` se válido.
- ❌ Retorna `false` se inválido.

---

### `isValidAmount (amount: string)`
Valida se o campo `amount` é um número decimal positivo.

- ✅ Exemplo válido: `"49.90"`
- ❌ Exemplo inválido: `"abc"`, `"-12.5"`

---

### `isValidPayload (payload: payloadDto1)`
Verifica se o payload possui campos obrigatórios preenchidos:

- `transaction_id`
- `amount`
- `timestamp`

---

### `hasBeenProcessed transactionId`
Verifica se o ID da transação já foi processado (para garantir **idempotência**).

---

### `processPayload (payload: payloadDto1)`
Lógica principal que decide:

- Confirmar a transação (`/confirmar`)
- Cancelar a transação (`/cancelar`)
- Rejeitar duplicatas ou payloads inválidos

Retorna um texto indicando a ação tomada.

---

### `webhookHandler (ctx: HttpContext)`
Função que processa requisições recebidas no endpoint `/webhook`.

Etapas:
1. Lê o corpo da requisição
2. Valida o token no header
3. Faz o parsing do JSON
4. Chama `processPayload`

---

### `app`
Define o roteamento da aplicação com Suave:

```fsharp
choose [
  path "/webhook" >=> POST >=> request webhookHandler
  NOT_FOUND "Not Found"
]
