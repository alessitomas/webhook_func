open Suave
open Suave.Filters
open Suave.Operators
open Suave.Successful
open Suave.RequestErrors
open System
open System.Net.Http
open System.Text
open System.Collections.Concurrent
open Newtonsoft.Json
open Types

let processedTransactions = ConcurrentDictionary<string, bool>()

let validateToken (token: string) : bool =
    printfn "Validando token: %s" token
    token = "meu-token-secreto"

let getBody (ctx: HttpContext) : string =
    let body = ctx.request.rawForm |> Encoding.UTF8.GetString
    printfn "Corpo da requisição recebido: %s" body
    body

let parsePayload (body: string) : TransactionPayload =
    try
        if String.IsNullOrWhiteSpace body then
            printfn "Erro: Corpo da requisição está vazio"
            { Event = None; TransactionId = None; Amount = None; Currency = None; Timestamp = None }
        else
            let rawPayload = JsonConvert.DeserializeObject<RawTransactionPayload>(body)
            printfn "Payload bruto deserializado: %A" rawPayload
            {
                Event = if isNull rawPayload.Event then None else Some rawPayload.Event
                TransactionId = if isNull rawPayload.TransactionId then None else Some rawPayload.TransactionId
                Amount = Some rawPayload.Amount
                Currency = if isNull rawPayload.Currency then None else Some rawPayload.Currency
                Timestamp = if isNull rawPayload.Timestamp then None else Some rawPayload.Timestamp
            }
    with ex ->
        printfn "Erro ao deserializar payload: %s" ex.Message
        printfn "JSON recebido: %s" body
        { Event = None; TransactionId = None; Amount = None; Currency = None; Timestamp = None }

let isValidTransaction (payload: TransactionPayload) : bool =
    match payload with
    | { Event = Some event; Amount = Some amount; Currency = Some currency }
        when event = "payment_success" && amount > 0.0 && currency = "BRL" ->
        printfn "Transação válida: event=%s, amount=%f, currency=%s" event amount currency
        true
    | _ ->
        printfn "Transação inválida: %A" payload
        false

let isDuplicateTransaction (transactionId: string) : bool =
    let isDuplicate = not (processedTransactions.TryAdd(transactionId, true))
    printfn "Verificando duplicidade para transactionId=%s: %b" transactionId isDuplicate
    isDuplicate

let makeHttpRequest (url: string) (payload: TransactionPayload) : Async<bool> =
    async {
        try
            use client = new HttpClient()
            let json = JsonConvert.SerializeObject({| transaction_id = payload.TransactionId.Value |})
            printfn "Enviando requisição para %s com payload: %s" url json
            let content = new StringContent(json, Encoding.UTF8, "application/json")
            let! response = client.PostAsync(url, content) |> Async.AwaitTask
            printfn "Resposta da requisição %s" url
            return response.IsSuccessStatusCode
        with ex ->
            printfn "Erro na requisição HTTP para %s: %s" url ex.Message
            return false
    }

let processValidTransaction (payload: TransactionPayload) (ctx: HttpContext) : Async<HttpContext option> =
    async {
        match payload.TransactionId with
        | Some transactionId ->
            if isDuplicateTransaction transactionId then
                let response = { Status = "duplicate transaction" }
                printfn "Transação duplicada detectada: %s" transactionId
                return! CONFLICT (JsonConvert.SerializeObject response) ctx
            else
                let! success = makeHttpRequest "http://127.0.0.1:5001/confirmar" payload
                if success then
                    let response = { Status = "ok" }
                    printfn "Transação confirmada com sucesso: %s" transactionId
                    return! OK (JsonConvert.SerializeObject response) ctx
                else
                    let response = { Status = "failed to confirm" }
                    printfn "Falha ao confirmar transação: %s" transactionId
                    return! ServerErrors.INTERNAL_ERROR (JsonConvert.SerializeObject response) ctx
        | None ->
            let response = { Status = "missing transaction_id" }
            printfn "Transação sem transaction_id"
            return! BAD_REQUEST (JsonConvert.SerializeObject response) ctx
    }

let processInvalidTransaction (payload: TransactionPayload) (ctx: HttpContext) : Async<HttpContext option> =
    async {
        match payload.TransactionId with
        | Some transactionId ->
            let! _ = makeHttpRequest "http://127.0.0.1:5001/cancelar" payload
            let response = { Status = "invalid transaction" }
            printfn "Transação cancelada: %s" transactionId
            return! BAD_REQUEST (JsonConvert.SerializeObject response) ctx
        | None ->
            let response = { Status = "missing transaction_id" }
            printfn "Transação sem transaction_id"
            return! BAD_REQUEST (JsonConvert.SerializeObject response) ctx
    }

let webhookHandler (ctx: HttpContext) : Async<HttpContext option> =
    async {
        printfn "Processando requisição webhook"
        let token = ctx.request.header "X-Webhook-Token"
        match token with
        | Choice1Of2 tokenValue ->
            if not (validateToken tokenValue) then
                let response = { Status = "invalid token" }
                printfn "Token inválido: %s" tokenValue
                return! UNAUTHORIZED (JsonConvert.SerializeObject response) ctx
            else
                let body = getBody ctx
                let payload = parsePayload body
                if isValidTransaction payload then
                    return! processValidTransaction payload ctx
                else
                    return! processInvalidTransaction payload ctx
        | Choice2Of2 _ ->
            let response = { Status = "missing token" }
            printfn "Token ausente na requisição"
            return! UNAUTHORIZED (JsonConvert.SerializeObject response) ctx
    }

let app : WebPart =
    choose [
        POST >=> path "/webhook" >=> webhookHandler
        NOT_FOUND "Route not found"
    ]

let cfg =
    { defaultConfig with
        bindings = [ HttpBinding.createSimple HTTP "127.0.0.1" 8080 ] }

[<EntryPoint>]
let main _ =
    printfn "Iniciando servidor Suave na porta 8080"
    startWebServer cfg app
    0

    