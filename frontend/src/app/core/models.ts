export type Guid = string;

export interface Product {
  id: string;              // SKU or product id (string for demo)
  name: string;
  category: string;
  price: number;           // EUR
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
