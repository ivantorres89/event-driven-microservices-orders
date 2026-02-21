workspace "Contoso Orders — Living Architecture" "C4 model (Structurizr DSL) generated from repo structure + draw.io diagram" {

  !identifiers hierarchical

  model {
    // --- People ---
    customer = person "Customer" "Uses the web app to place orders and track status updates."

    // --- Software System ---
    contosoOrders = softwareSystem "Contoso Orders Platform" "Event-driven order processing with real-time notifications." {

      // --- Containers (C4 Level 2) ---
      spa = container "Client SPA" "Web UI where customers browse and place orders." "Angular (web)"

      apim = container "API Gateway" "Public perimeter: authentication, rate limiting, request forwarding." "Azure API Management"
      ingress = container "Ingress" "Routes incoming traffic to Kubernetes services." "NGINX Ingress Controller (AKS)"

      orderAccept = container "Order Accept API" "Accepts orders quickly; creates correlation ID; enqueues work; stores initial workflow state." ".NET API (AKS)"
      orderProcess = container "Order Process Worker" "Consumes accepted orders; persists to SQL; publishes processed events; updates workflow state." ".NET Worker (AKS)"
      orderNotification = container "Order Notification Service" "WebSocket/SignalR hub that pushes order status updates to customers." ".NET (AKS)"

      acceptedQueue = container "Queue: order.accepted" "Queue for accepted orders (async processing)." "Azure Service Bus (cloud) / RabbitMQ (local)" {
        tags "Queue"
      }

      processedQueue = container "Queue: order.processed" "Queue for processed orders (used to notify clients)." "Azure Service Bus (cloud) / RabbitMQ (local)" {
        tags "Queue"
      }

      redis = container "Redis" "Workflow state + Pub/Sub backplane for SignalR scale-out (not system of record)." "Azure Cache for Redis (cloud) / Redis (local)" {
        tags "DataStore"
      }

      sql = container "SQL Database" "System of record for orders (OLTP)." "Azure SQL Database (cloud) / SQL Server (local)" {
        tags "Database"
      }
    }

    // --- Relationships (reference containers via the softwareSystem scope) ---
    customer -> contosoOrders.spa "Uses" "HTTPS"
    contosoOrders.spa -> contosoOrders.apim "POST /api/orders (auth)" "HTTPS"
    contosoOrders.apim -> contosoOrders.ingress "Forwards authenticated traffic" "HTTPS"

    // WebSocket realtime channel
    contosoOrders.spa -> contosoOrders.orderNotification "Connects for real-time updates" "WSS"
    contosoOrders.orderNotification -> contosoOrders.spa "Pushes status updates" "WSS"

    // Request path into AKS
    contosoOrders.ingress -> contosoOrders.orderAccept "Routes /api/orders" "HTTP"

    // Event-driven processing
    contosoOrders.orderAccept -> contosoOrders.acceptedQueue "Publishes OrderAccepted" ""
    contosoOrders.orderProcess -> contosoOrders.acceptedQueue "Consumes OrderAccepted" ""
    contosoOrders.orderProcess -> contosoOrders.processedQueue "Publishes order.processed" ""
    contosoOrders.orderNotification -> contosoOrders.processedQueue "Consumes order.processed" ""

    // State + persistence
    contosoOrders.orderAccept -> contosoOrders.redis "Sets correlation/status (TTL)" ""
    contosoOrders.orderProcess -> contosoOrders.redis "Updates workflow status" ""
    contosoOrders.orderNotification -> contosoOrders.redis "Reads mapping/status; fan-out notifications" ""
    contosoOrders.orderProcess -> contosoOrders.sql "Persists order" "TDS"
  }

  views {
    // C4 Level 1
    systemContext contosoOrders "SystemContext" {
      include *
      autolayout lr
    }

    // C4 Level 2
    container contosoOrders "Containers" {
      include *
      autolayout lr
    }

    // Sequence / flow view (useful to explain the workflow quickly)
    dynamic contosoOrders "OrderFlow" {
      title "Order flow (happy path)"

      customer -> contosoOrders.spa "Uses"
      contosoOrders.spa -> contosoOrders.apim "POST /api/orders"
      contosoOrders.apim -> contosoOrders.ingress "Forward"
      contosoOrders.ingress -> contosoOrders.orderAccept "Route request"

      contosoOrders.orderAccept -> contosoOrders.redis "Set correlation/status"
      contosoOrders.orderAccept -> contosoOrders.acceptedQueue "Publish OrderAccepted"

      contosoOrders.orderProcess -> contosoOrders.acceptedQueue "Consume"
      contosoOrders.orderProcess -> contosoOrders.sql "Persist order"
      contosoOrders.orderProcess -> contosoOrders.redis "Update status"
      contosoOrders.orderProcess -> contosoOrders.processedQueue "Publish order.processed"

      contosoOrders.orderNotification -> contosoOrders.processedQueue "Consume"
      contosoOrders.orderNotification -> contosoOrders.redis "Lookup mapping/status"
      contosoOrders.orderNotification -> contosoOrders.spa "Push order status"

      autolayout lr
    }

    styles {
      element "Person" {
        shape Person
      }

      element "Database" {
        shape Cylinder
      }

      element "Queue" {
        shape Pipe
      }

      element "DataStore" {
        shape Cylinder
      }
    }

    theme default
  }
}
