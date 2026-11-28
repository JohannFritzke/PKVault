/** Display name of a login provider of the PokéHome server. */
export const cloudProviderLabel = (provider?: string | null) => provider === 'github' ? 'GitHub' : provider === 'google' ? 'Google' : provider ?? '';
