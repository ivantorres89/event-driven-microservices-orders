export type Guid = string;

export interface Product {
  id: string;              // ExternalProductId / SKU (string for demo)
  name: string;
  category: string;

  // MeasureUp-like catalog fields (mocked now; will come from backend later)
  vendor?: string;
  imageUrl?: string;       // CDN image URL
  discountPercent?: number; // 0..100
  billingPeriod?: string;   // Monthly | Annual (demo)
  isSubscription?: boolean;

  price: number;           // EUR (discounted/current price)
  stock: number;
}

export interface CartLine {
  product: Product;
  quantity: number;
}

export type OrderWorkflowStatus = 'Accepted' | 'Processing' | 'Completed';

export interface OrderWorkflowState {
  status: OrderWorkflowStatus;
  orderId: number | null;
}

export interface OrderStatusNotification {
  correlationId: Guid;
  status: OrderWorkflowStatus;
  orderId: number | null;
}

export interface Order {
  id: number;
  correlationId: Guid;
  createdAtUtc: string;
  lines: Array<{
    productId: string;
    productName: string;
    unitPrice: number;
    quantity: number;
  }>;
}

export interface ActiveOrder {
  correlationId: Guid;
  createdAtUtc: string;
  status: OrderWorkflowStatus;
  orderId: number | null;
  lines: Order['lines'];
}
