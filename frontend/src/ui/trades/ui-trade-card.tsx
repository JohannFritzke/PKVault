import { ArrowLeftRightIcon } from 'lucide-react';
import type React from 'react';
import classes from './ui-trade-card.module.css';

export type UITradePokemonProps = {
    img?: React.ReactNode;
    name?: React.ReactNode;
    meta?: React.ReactNode;
    /** Problem with this pokémon (illegal...). */
    warning?: React.ReactNode;
    /** Shown instead of a pokémon (nothing offered back yet...). */
    emptyLabel?: React.ReactNode;
    /** A button in the empty slot (choose a pokémon to offer back). */
    emptyAction?: React.ReactNode;
};

export const UITradePokemon: React.FC<UITradePokemonProps> = ({ img, name, meta, warning, emptyLabel, emptyAction }) => {
    if (emptyLabel !== undefined || emptyAction !== undefined) {
        return <div className={classes.pokemon} data-empty='true'>{emptyAction ?? emptyLabel}</div>;
    }

    return <div className={classes.pokemon}>
        {img}
        <div className={classes.pokemonBody}>
            <span className={classes.pokemonName}>{name}</span>
            {meta && <span className={classes.pokemonMeta}>{meta}</span>}
            {warning && <span className={classes.pokemonWarning}>{warning}</span>}
        </div>
    </div>;
};

/**
 * - you: waiting for the current user (amber)
 * - friend: waiting for the friend (neutral)
 * - done: completed (green)
 * - failed: declined, cancelled or expired (red, the text says why)
 */
export type UITradeTone = 'you' | 'friend' | 'done' | 'failed';

type UITradeStatusProps = {
    tone: UITradeTone;
    children: React.ReactNode;
};

export const UITradeStatus: React.FC<UITradeStatusProps> = ({ tone, children }) => {
    return <span className={classes.status} data-tone={tone}>
        <span className={classes.statusDot} />
        {children}
    </span>;
};

type UITradeCardProps = {
    tone: UITradeTone;
    people: React.ReactNode;
    status: React.ReactNode;
    date: React.ReactNode;
    offered: React.ReactNode;
    countered: React.ReactNode;
    messages?: React.ReactNode[];
    actions?: React.ReactNode;
};

export const UITradeCard: React.FC<UITradeCardProps> = ({ tone, people, status, date, offered, countered, messages, actions }) => {
    return <div className={classes.uiTradeCard} data-tone={tone}>
        <div className={classes.header}>
            <span className={classes.people}>{people}</span>
            {status}
            <span className={classes.date}>{date}</span>
        </div>

        <div className={classes.pokemons}>
            {offered}
            <div className={classes.arrow}><ArrowLeftRightIcon size={18} /></div>
            {countered}
        </div>

        {messages?.filter(Boolean).map((message, i) => <div key={i} className={classes.message}>{message}</div>)}

        <div className={classes.actions}>{actions}</div>
    </div>;
};
