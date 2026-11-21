import { clsx } from 'clsx';
import React from 'react';
import classes from './ui-game-icon.module.css';

type UIGameIconProps = {
    src: string;
    /** Tile size in px. The image is drawn at 32px inside (pixel-art icons are 32px). */
    size?: number;
    className?: string;
};

/**
 * Game icon inside a neutral tile.
 * Small pixel-art icons (<= 48px) are drawn at their native 32px with crisp pixels,
 * instead of being stretched (which made them look blurry next to the UI).
 */
export const UIGameIcon: React.FC<UIGameIconProps> = ({ src, size = 44, className }) => {
    const [ pixel, setPixel ] = React.useState(true);

    return <span
        className={clsx(classes.tile, className)}
        style={{ '--game-icon-size': `${size}px` } as React.CSSProperties}
    >
        <img
            src={src}
            alt=""
            className={classes.img}
            data-pixel={pixel}
            onLoad={e => setPixel(e.currentTarget.naturalWidth <= 48)}
        />
    </span>;
};
