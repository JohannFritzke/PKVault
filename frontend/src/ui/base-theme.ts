import { rem, type CSSVariablesResolver, type MantineThemeOverride } from '@mantine/core';

const customTheme = {
  game: {
    blue: "#2446A1",
    red: "#A14624",
    yellow: "#EAB000",
    gold: "#9C8B65",
    silver: "#747A78",
    crystal: "#6B4777",
    sapphire: "#104070",
    emerald: "#457865",
    ruby: "#781810",
    za: "#32796B",
  },
  type: {
    normal: '#9FA19F',
    fighting: '#FF8000',
    fly: '#81B9EF',
    poison: '#9141CB',
    ground: '#915121',
    rock: '#AFA981',
    bug: '#91A119',
    ghost: '#704170',
    steel: '#60A1B8',
    fire: '#E62829',
    water: '#2980EF',
    grass: '#3FA129',
    electric: '#FAC000',
    psychic: '#EF4179',
    ice: '#3FD8FF',
    dragon: '#5060E1',
    dark: '#50413F',
    fairy: '#EF70EF',
    stellar: '#F6A516',
    unknown: '#64A894',
  },
  stats: {
    hp: '#4caf70',
    atk: '#f08030',
    def: '#f0c030',
    spa: '#6890f0',
    spd: '#78c850',
    spe: '#f85888',
  },
  contest: {
    cool: '#EA7254',
    beauty: '#63B9D6',
    cute: '#E681B3',
    smart: '#5DC27B',
    tough: '#DEC02C',
  },
  misc: {
    genderMale: '#00C6AD',
    genderFemale: '#FF4273',
    markBlue: '#7077E4',
    markPink: '#F681DD',
  },
};

type CustomTheme = typeof customTheme;

declare module '@mantine/core' {
  // eslint-disable-next-line @typescript-eslint/no-empty-object-type
  export interface MantineThemeOther extends CustomTheme {
  }
}

export const baseTheme = {
  // shadcn-like visual language: neutral zinc surfaces, 1px borders, soft shadows, ~8px radius
  fontFamily: "Arial, Helvetica, sans-serif",
  fontFamilyMonospace: "ui-monospace, Consolas, 'Courier New', monospace",
  headings: {
    fontFamily: "Arial, Helvetica, sans-serif",
    fontWeight: '600',
  },
  defaultRadius: 'md',
  primaryShade: { light: 6, dark: 6 },
  primaryColor: 'primary',
  white: '#ffffff', // --mantine-color-white, card bg (light)
  black: '#09090b', // --mantine-color-text (light)
  // autoContrast: true,
  colors: {
    white: [
      '#ffffff',
      '#ffffff',
      '#fcfcfc',
      '#fafafa',  // popover bg (light)
      '#fafafa',  // --mantine-color-white-4, --mantine-color-body (light)
      '#ffffff',  // --mantine-color-white-5
      '#f4f4f5',  // --mantine-color-white-6
      '#e4e4e7',
      '#52525b',
      '#09090b'
    ],
    gray: [
      '#f4f4f5',  // Button hover
      '#f4f4f5',
      '#e4e4e7',  // Progress bg
      '#e4e4e7',  // --mantine-color-gray-3, card border
      '#d4d4d8',
      '#a1a1aa',
      '#71717a',
      '#52525b',
      '#27272a',
      '#09090b'
    ],
    // zinc-based dark scale: 0 text, 2 dimmed, 4 border, 6 popover/active, 8 paper, 9 body
    dark: [
      '#fafafa',
      '#d4d4d8',
      '#a1a1aa',
      '#71717a',
      '#3f3f46',
      '#27272a',
      '#202023',
      '#18181b',
      '#111113',
      '#09090b'
    ],
    // #B6634E / #934E3D
    primary: [
      '#f5e5e4',
      '#f0e8e2',
      '#ecdeda',
      '#d5a89d',
      '#cd9688',
      '#cd9688',
      '#B6634E',
      '#934E3D',
      '#814435',
      '#150905'
    ],
    red: [
      "#ffeeea",
      "#f9ddd9",
      "#e9bab3",
      "#da958a",
      "#cd7668",
      "#c96b5c",
      "#c35746",
      "#ac4737",
      "#9a3e2f",
      "#883226"
    ],
    blue: [
      "#eaf5ff",
      "#dae7f6",
      "#b8cbe3",
      "#92aed1",
      "#7395c2",
      "#5c84b8",
      "#527eb6",
      "#426ca1",
      "#376091",
      "#275382"
    ],
  },
  fontSizes: {
    xs: rem(11),
    sm: rem(12),
    md: rem(14),
    lg: rem(16),
    xl: rem(20),
  },
  shadows: {
    xs: `0 ${rem(1)} ${rem(2)} rgba(0, 0, 0, 0.04)`,
    sm: `0 ${rem(1)} ${rem(2)} rgba(0, 0, 0, 0.06)`,
    md: `0 ${rem(4)} ${rem(6)} ${rem(-1)} rgba(0, 0, 0, 0.08), 0 ${rem(2)} ${rem(4)} ${rem(-2)} rgba(0, 0, 0, 0.06)`,
    lg: `0 ${rem(10)} ${rem(15)} ${rem(-3)} rgba(0, 0, 0, 0.1), 0 ${rem(4)} ${rem(6)} ${rem(-4)} rgba(0, 0, 0, 0.08)`,
    xl: `0 ${rem(20)} ${rem(25)} ${rem(-5)} rgba(0, 0, 0, 0.1), 0 ${rem(8)} ${rem(10)} ${rem(-6)} rgba(0, 0, 0, 0.08)`,
  },
  spacing: {
    xs: rem(4),
    sm: rem(6),
    md: rem(12),
    lg: rem(16),
    xl: rem(32)
  },
  radius: {
    xs: rem(4),
    sm: rem(6),
    md: rem(8),
    lg: rem(12),
    xl: rem(16),
  },
  breakpoints: {
    xs: '36rem', // rem(576),
    sm: '48rem', // rem(768),
    md: '64rem', // rem(1024),
    lg: '80rem', // rem(1280),
    xl: '93rem', // rem(1488),
  },
  other: customTheme,
} satisfies MantineThemeOverride;

export const cssVariablesResolver: CSSVariablesResolver = (theme) => ({
  variables: {
  },
  light: {
    '--button-hover': 'var(--mantine-color-gray-0)',
    '--mantine-color-disabled': 'var(--mantine-color-white-5)',
  },
  dark: {
    '--mantine-color-disabled-color': 'var(--mantine-color-dark-1)',
  },
});
