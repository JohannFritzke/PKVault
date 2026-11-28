import type React from 'react';
import classes from './ui-details-info-box.module.css';

export const UIDetailsInfoBoxes: React.FC<{ children: React.ReactNode }> = ({ children }) => {
    return <div className={classes.boxes}>{children}</div>;
};

type UIDetailsInfoBoxProps = {
    label: React.ReactNode;
    value: React.ReactNode;
    hint?: React.ReactNode;
    tone?: 'good' | 'warning';
};

/** A small labelled fact (where the pokémon is, legality...). */
export const UIDetailsInfoBox: React.FC<UIDetailsInfoBoxProps> = ({ label, value, hint, tone }) => {
    return <div className={classes.box}>
        <span className={classes.label}>{label}</span>
        <span className={classes.value} data-tone={tone}>{value}</span>
        {hint && <span className={classes.hint}>{hint}</span>}
    </div>;
};
