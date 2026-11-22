import { createFileRoute } from "@tanstack/react-router";
import { TradesPage } from '../pages/trades';

export const Route = createFileRoute("/trades")({
  component: TradesPage,
});
