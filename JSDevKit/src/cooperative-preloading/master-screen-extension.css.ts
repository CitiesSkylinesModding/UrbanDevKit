// noinspection CssUnresolvedCustomProperty,CssUnusedSymbol
export const style = /* language=CSS */ `
.udk-cooperative-preloading-spin {
    animation: udk-cooperative-preloading-spin 1.2s cubic-bezier(.45, .85, .60, .30) infinite;
}

.udk-cooperative-preloading-icon-failed {
    --iconColor: var(--warningColor);
}

@keyframes udk-cooperative-preloading-spin {
    from {
        transform: rotate(0deg);
    }
    to {
        transform: rotate(360deg);
    }
}`;
