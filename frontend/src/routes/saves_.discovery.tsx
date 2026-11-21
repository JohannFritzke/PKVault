import { createFileRoute } from "@tanstack/react-router";
import { SaveDiscoveryPage } from '../pages/save-discovery';

export const Route = createFileRoute("/saves_/discovery")({
  component: SaveDiscoveryPage,
});
