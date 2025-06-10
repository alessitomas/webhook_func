module Types
open Newtonsoft.Json

type RawTransactionPayload = {
    [<JsonProperty("event")>]
    Event: string
    [<JsonProperty("transaction_id")>]
    TransactionId: string
    [<JsonProperty("amount")>]
    Amount: float
    [<JsonProperty("currency")>]
    Currency: string
    [<JsonProperty("timestamp")>]
    Timestamp: string
}

type TransactionPayload = {
    Event: string option
    TransactionId: string option
    Amount: float option
    Currency: string option
    Timestamp: string option
}

type Response = { Status: string }
